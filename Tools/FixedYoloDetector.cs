using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using Docnet.Core;
using Docnet.Core.Models;
using System.Runtime.InteropServices;

namespace OB.Tools
{
    /// <summary>
    /// 偵測結果物件，包含像素座標與物理尺寸
    /// </summary>
    public class Prediction
    {
        public SKRect Box { get; set; }          // 在整張 PDF 上的像素座標
        public string Label { get; set; }        // 類別名稱
        public float Confidence { get; set; }     // 置信度 (0.0 ~ 1.0)

        // --- 物理尺寸屬性 ---
        public float RealWidthMm => RealWidthCm * 10f;
        public float RealHeightMm => RealHeightCm * 10f;
        public float RealWidthCm { get; set; }
        public float RealHeightCm { get; set; }
    }

    public class FixedYoloDetector : IDisposable
    {
        private InferenceSession? _session;
        private string[]? _labels;
        private int _inputWidth;
        private int _inputHeight;
        private string _inputName = "images";

        // PDF 點數轉公釐的常數 (1 point = 25.4/72 mm)
        private const double PointToMm = 0.3527777778;

        public FixedYoloDetector()
        {
            // 初始化時，嘗試從 Settings 讀取舊有的類別紀錄
            if (!string.IsNullOrEmpty(OB.Default.mYoloLabels))
            {
                _labels = OB.Default.mYoloLabels.Split(',');
            }
        }

        /// <summary>
        /// 加載 ONNX 模型
        /// </summary>
        public async Task LoadModelAsync(string modelPath)
        {
            await Task.Run(() =>
            {
                _session = new InferenceSession(modelPath);

                // 解析輸入節點 (通常是 1x3x640x640)
                var inputMeta = _session.InputMetadata.First();
                _inputName = inputMeta.Key;
                _inputWidth = inputMeta.Value.Dimensions[3];
                _inputHeight = inputMeta.Value.Dimensions[2];

                // 提取類別標籤
                var metadata = _session.ModelMetadata.CustomMetadataMap;
                if (metadata.TryGetValue("names", out string? namesJson))
                {
                    _labels = ParseYoloLabels(namesJson);
                }
                else
                {
                    var outputMeta = _session.OutputMetadata.First().Value;
                    int classCount = outputMeta.Dimensions[1] - 4;
                    _labels = Enumerable.Range(0, classCount).Select(i => $"Class_{i}").ToArray();
                }

                if (_labels != null && _labels.Length > 0)
                {
                    OB.Default.mYoloLabels = string.Join(",", _labels);
                    OB.Default.Save();
                }

                Console.WriteLine($"[Model Loaded] Input: {_inputWidth}x{_inputHeight}, Classes: {_labels?.Length}");
            });
        }

        /// <summary>
        /// 對 PDF 進行偵測並計算目標物理尺寸
        /// </summary>
        /// <param name="pdfPath">PDF 路徑</param>
        /// <param name="pageIndex">頁碼 (0 為第一頁)</param>
        /// <param name="scaling">渲染放大倍率 (建議 2.0d ~ 3.0d 以提高辨識率)</param>
        public async Task<List<Prediction>> DetectPdfAsync(string pdfPath, int pageIndex = 0, float confidenceThreshold = 0.45f, double scaling = 2.5d)
        {
            if (_session == null) throw new InvalidOperationException("Model not loaded.");

            return await Task.Run(() =>
            {
                var allPredictions = new List<Prediction>();

                // 1. 獲取 PDF 原始物理尺寸 (以 Points 為單位)
                double rawPointsWidth, rawPointsHeight;
                using (var library = DocLib.Instance)
                {
                    using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1.0d)); // 1.0d 代表原始 Points
                    using var pageReader = docReader.GetPageReader(pageIndex);
                    rawPointsWidth = pageReader.GetPageWidth();
                    rawPointsHeight = pageReader.GetPageHeight();
                }

                // 2. 渲染用於偵測的高解析度點陣圖
                using var libraryRender = DocLib.Instance;
                using var docReaderRender = libraryRender.GetDocReader(pdfPath, new PageDimensions(scaling));
                using var pageReaderRender = docReaderRender.GetPageReader(pageIndex);

                var renderWidth = pageReaderRender.GetPageWidth();
                var renderHeight = pageReaderRender.GetPageHeight();
                var rawBgrBytes = pageReaderRender.GetImage(); // BGRA 格式

                // --- 計算 Pixels Per CM (PPCM) ---
                // 物理寬度 (cm) = Points * 0.35277 / 10
                double physicalWidthCm = (rawPointsWidth * PointToMm) / 10.0;
                double pixelsPerCm = renderWidth / physicalWidthCm;

                // 3. 轉為 SkiaSharp Bitmap
                using var fullImage = new SKBitmap();
                var info = new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                GCHandle handle = GCHandle.Alloc(rawBgrBytes, GCHandleType.Pinned);
                fullImage.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => { handle.Free(); });

                // 4. 切塊邏輯 (Slicing)
                int tileSize = _inputWidth;
                int overlap = (int)(tileSize * 0.25);
                int step = tileSize - overlap;

                for (int y = 0; y < renderHeight; y += step)
                {
                    for (int x = 0; x < renderWidth; x += step)
                    {
                        int currX = Math.Min(x, renderWidth - tileSize);
                        int currY = Math.Min(y, renderHeight - tileSize);
                        if (currX < 0 || currY < 0) continue;

                        using var tile = new SKBitmap(tileSize, tileSize);
                        fullImage.ExtractSubset(tile, SKRectI.Create(currX, currY, tileSize, tileSize));

                        var tileResults = PerformInference(tile, confidenceThreshold);

                        // 座標映射 (Local Tile -> Global PDF)
                        foreach (var res in tileResults)
                        {
                            res.Box = new SKRect(
                                res.Box.Left + currX,
                                res.Box.Top + currY,
                                res.Box.Right + currX,
                                res.Box.Bottom + currY
                            );
                            allPredictions.Add(res);
                        }
                        if (x + tileSize >= renderWidth) break;
                    }
                    if (y + tileSize >= renderHeight) break;
                }

                // 5. NMS 與物理尺寸計算
                var finalResults = ApplyNMS(allPredictions, 0.45f);

                foreach (var pred in finalResults)
                {
                    // 計算真實公分 (像素 / PPCM)
                    pred.RealWidthCm = (float)(pred.Box.Width / pixelsPerCm);
                    pred.RealHeightCm = (float)(pred.Box.Height / pixelsPerCm);
                }

                return finalResults;
            });
        }

        private List<Prediction> PerformInference(SKBitmap bitmap, float threshold)
        {
            var tensor = BitmapToTensor(bitmap);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };

            using var results = _session!.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();

            return ParseYoloV11Output(outputTensor, threshold);
        }

        private unsafe DenseTensor<float> BitmapToTensor(SKBitmap bitmap)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
            byte* ptr = (byte*)bitmap.GetPixels().ToPointer();
            int rowBytes = bitmap.RowBytes;

            for (int y = 0; y < _inputHeight; y++)
            {
                byte* row = ptr + (y * rowBytes);
                for (int x = 0; x < _inputWidth; x++)
                {
                    // SkiaSharp BGRA 轉 RGB Tensor
                    tensor[0, 0, y, x] = row[x * 4 + 2] / 255.0f; // R
                    tensor[0, 1, y, x] = row[x * 4 + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = row[x * 4 + 0] / 255.0f; // B
                }
            }
            return tensor;
        }

        private List<Prediction> ParseYoloV11Output(Tensor<float> output, float threshold)
        {
            var results = new List<Prediction>();
            int numClasses = output.Dimensions[1] - 4;
            int numAnchors = output.Dimensions[2];

            for (int i = 0; i < numAnchors; i++)
            {
                float maxConfidence = 0;
                int labelId = -1;

                for (int j = 0; j < numClasses; j++)
                {
                    float conf = output[0, j + 4, i];
                    if (conf > maxConfidence)
                    {
                        maxConfidence = conf;
                        labelId = j;
                    }
                }

                if (maxConfidence >= threshold)
                {
                    float cx = output[0, 0, i];
                    float cy = output[0, 1, i];
                    float w = output[0, 2, i];
                    float h = output[0, 3, i];

                    results.Add(new Prediction
                    {
                        Box = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2),
                        Label = _labels != null && labelId < _labels.Length ? _labels[labelId] : labelId.ToString(),
                        Confidence = maxConfidence
                    });
                }
            }
            return results;
        }

        private List<Prediction> ApplyNMS(List<Prediction> predictions, float iouThreshold)
        {
            var sorted = predictions.OrderByDescending(p => p.Confidence).ToList();
            var result = new List<Prediction>();

            while (sorted.Count > 0)
            {
                var current = sorted[0];
                result.Add(current);
                sorted.RemoveAt(0);

                for (int i = sorted.Count - 1; i >= 0; i--)
                {
                    float iou = CalculateIoU(current.Box, sorted[i].Box);
                    if (iou > iouThreshold)
                    {
                        sorted.RemoveAt(i);
                    }
                }
            }
            return result;
        }

        private float CalculateIoU(SKRect rect1, SKRect rect2)
        {
            if (!rect1.IntersectsWith(rect2)) return 0;
            var intersect = SKRect.Intersect(rect1, rect2);
            float intersectArea = intersect.Width * intersect.Height;
            float unionArea = (rect1.Width * rect1.Height) + (rect2.Width * rect2.Height) - intersectArea;
            return intersectArea / unionArea;
        }

        private string[] ParseYoloLabels(string json)
        {
            try
            {
                return json.Trim('{', '}')
                           .Split(',')
                           .Select(x => x.Split(':')[1].Trim(' ', '\'', '\"'))
                           .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
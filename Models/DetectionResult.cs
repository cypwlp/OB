using System.Collections.Generic;
using System.Linq;

namespace OB.Models
{
    public class DetectionResult
    {
        // 分类结果（适用于分类模型）
        public List<ClassificationPrediction> Classifications { get; set; }

        // 检测结果（适用于目标检测模型）
        public List<BoundingBox> Boxes { get; set; }

        // 分割掩码（适用于分割模型）
        public byte[][] Masks { get; set; }

        // 原始输出张量（保留原始数据，供调试或自定义处理）
        public object RawOutput { get; set; }
    }

    public class ClassificationPrediction
    {
        public string Label { get; set; }
        public float Confidence { get; set; }
    }

    public class BoundingBox
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Label { get; set; }
        public float Confidence { get; set; }
        public byte[,] Mask { get; set; }           // 最终掩码
        public float[] MaskCoeffs { get; set; }     // 临时存储掩码系数
    }

    public class OnnxAnalysisResult : OnnxModelInfo
    {
        public long FileSize { get; set; }
        public List<string> OperatorTypes { get; set; } = new List<string>();
        public long EstimatedParameterCount { get; set; }
        public bool HasDynamicInput => Inputs?.Any(i => i.HasDynamicDimension) ?? false;
        public bool HasDynamicOutput => Outputs?.Any(o => o.HasDynamicDimension) ?? false;
        public string CompatibilityNotes { get; set; }
    }
}
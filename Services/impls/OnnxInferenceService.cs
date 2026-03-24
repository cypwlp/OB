using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OB.Services.impls
{
    public class OnnxInferenceService : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly IPreprocessor _preprocessor;
        private readonly IPostprocessor _postprocessor;

        public OnnxInferenceService(string modelPath, IPreprocessor preprocessor, IPostprocessor postprocessor)
        {
            _session = new InferenceSession(modelPath);
            _preprocessor = preprocessor;
            _postprocessor = postprocessor;
        }

        public async Task<DetectionResult> PredictAsync(object image)
        {
            var inputs = _preprocessor.Process(image);
            var inputsList = inputs.Select(kv => NamedOnnxValue.CreateFromTensor(kv.Key, kv.Value)).ToList();

            using (var results = _session.Run(inputsList))
            {
                return _postprocessor.Process(results);
            }
        }

        public async Task<List<DetectionResult>> PredictBatchAsync(IEnumerable<object> images)
        {
            var results = new List<DetectionResult>();
            foreach (var img in images)
            {
                results.Add(await PredictAsync(img));
            }
            return results;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
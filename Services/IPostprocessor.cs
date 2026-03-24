using Microsoft.ML.OnnxRuntime;
using OB.Models;
using System.Collections.Generic;


namespace OB.Services
{
    public interface IPostprocessor
    {
        DetectionResult Process(IReadOnlyList<NamedOnnxValue> outputs);
    }
}

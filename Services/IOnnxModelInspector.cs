using Microsoft.ML.OnnxRuntime;
using OB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IOnnxModelInspector
    {
         Task<OnnxModelInfo> GetModelInfoAsync(InferenceSession session, string modelPath);
    }
}

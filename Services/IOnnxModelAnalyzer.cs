using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IOnnxModelAnalyzer
    {
        Task<Models.OnnxAnalysisResult> AnalyzeAsync(string modelPath, bool deepAnalysis = false);
    }
}

using Avalonia;
using System.Collections.Generic;
using System.Linq;

namespace OB.Models
{
    public enum AnnotationType { BoundingBox, Polygon }

    public class Annotation
    {
        public AnnotationType Type { get; set; }
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public List<Point> Points { get; set; } = new List<Point>();

        public string DisplayText => $"{ClassName} ({Type})";

        public Rect GetBoundingBox()
        {
            if (Points.Count == 0) return default;
            double minX = Points.Min(p => p.X);
            double minY = Points.Min(p => p.Y);
            double maxX = Points.Max(p => p.X);
            double maxY = Points.Max(p => p.Y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
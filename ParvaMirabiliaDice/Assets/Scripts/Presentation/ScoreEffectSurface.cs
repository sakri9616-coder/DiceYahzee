using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// The drawing surface the score and attack presenters write their strokes into.
    ///
    /// The presenters own the judgement side of an effect: which hand formed, which dice take
    /// part, the tier, the timing and the geometry that follows from them. This boundary carries
    /// only the shapes, so a different renderer can replace the shipping one without touching the
    /// timing or the numbers a hand produces. ScoreHandEffectGraphic is the shipped renderer.
    /// </summary>
    internal interface IScoreEffectSurface
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        void Clear();
        void Flush();
        void Line(Vector2 from, Vector2 to, float width, Color color);
        void Ring(Vector2 center, float radius, float width, Color color, int steps);
        void Ellipse(Vector2 center, float radiusX, float radiusY, float width, Color color, int steps);
    }
}

using UnityEngine;
using UnityEngine.UI;

// The artist's ornate frame around a UI element, built by GameBuilder.AddFrame from 12 anchored pieces: the top and
// bottom edges are an end piece, a stretching strip, the centered ornament, another strip and the other end piece (so
// the corners and ornaments never stretch, only the plain line between them), and the sides stretch. Anchors do the
// layout; this only tints every piece at once.
public class OrnateFrame : MonoBehaviour
{
    public Image[] pieces;

    public Color Color
    {
        get => pieces.Length > 0 ? pieces[0].color : Color.white;
        set
        {
            foreach (Image piece in pieces) piece.color = value; // a Graphic only redraws when its color changes
        }
    }
}

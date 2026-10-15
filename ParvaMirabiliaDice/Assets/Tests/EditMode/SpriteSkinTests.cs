using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ParvaMirabilia.Tests
{
    /// <summary>
    /// The skin underneath every widget: <c>UiFactory.RoundedSprite</c>.
    ///
    /// A shape that comes out wrong is invisible to every other test in this suite - they read
    /// positions and labels, never pixels - and this one was wrong: nine-slicing the sprite made
    /// Unity scale the corner by the size of whatever it landed on, so the dice photographed as
    /// circles and the buttons as ellipses. The sprite is therefore checked here, before the
    /// renderer gets a chance to stretch it, and the alpha map is written to
    /// logs/rounded-sprite.txt so the shape can be read at a glance.
    /// </summary>
    public sealed class SpriteSkinTests
    {
        private static Sprite Rounded(int width, int height)
        {
            // UiFactory is internal to the presentation assembly, so it is reached the same way
            // the PlayMode tests reach DiceView - by name, across every loaded assembly, because
            // the assembly name is a build detail this test should not depend on.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type factory = assembly.GetType("ParvaMirabilia.Presentation.UiFactory", false);
                if (factory == null)
                    continue;

                var method = factory.GetMethod("RoundedSprite",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(int), typeof(int) }, null);
                Assert.That(method, Is.Not.Null, "UiFactory.RoundedSprite was not found");
                return method.Invoke(null, new object[] { width, height }) as Sprite;
            }

            Assert.Fail("UiFactory was not found");
            return null;
        }

        private static Sprite Property(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type factory = assembly.GetType("ParvaMirabilia.Presentation.UiFactory", false);
                if (factory == null)
                    continue;

                var property = factory.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                Assert.That(property, Is.Not.Null, "UiFactory." + name + " was not found");
                return property.GetValue(null, null) as Sprite;
            }

            Assert.Fail("UiFactory was not found");
            return null;
        }

        private static float Alpha(Texture2D texture, int x, int y)
        {
            return texture.GetPixel(x, y).a;
        }

        /// <summary>The table lighting: what keeps a flat field of colour from reading as debug.</summary>
        [Test]
        public void VignetteSprite_IsClearInTheMiddleAndDarkAtTheEdges()
        {
            var texture = Property("VignetteSprite").texture;
            int w = texture.width;
            int h = texture.height;

            Assert.That(Alpha(texture, w / 2, h / 2), Is.LessThan(0.05f),
                "the middle of the screen is darkened, so the dice are not what the eye lands on");
            Assert.That(Alpha(texture, 0, 0), Is.GreaterThan(0.4f),
                "the corners are clear, so the vignette darkens nothing");
        }

        [TestCase(84, 84)]        // a die face
        [TestCase(140, 56)]       // a button
        [TestCase(820, 700)]      // the rules panel, large enough to be generated small
        public void RoundedSprite_CutsTheCornersAndKeepsTheFlatSidesFlat(int width, int height)
        {
            var texture = Rounded(width, height).texture;
            int w = texture.width;
            int h = texture.height;

            // The four corners are outside the shape...
            Assert.That(Alpha(texture, 0, 0), Is.LessThan(0.1f), "top-left corner is not cut");
            Assert.That(Alpha(texture, w - 1, 0), Is.LessThan(0.1f), "top-right corner is not cut");
            Assert.That(Alpha(texture, 0, h - 1), Is.LessThan(0.1f), "bottom-left corner is not cut");
            Assert.That(Alpha(texture, w - 1, h - 1), Is.LessThan(0.1f),
                "bottom-right corner is not cut");

            // ...and a point well inside the corner arc is still solid, which is what separates a
            // rounded rectangle from a circle: a circle of this radius would have nothing there.
            int inside = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(w, h) * 0.15f));
            Assert.That(Alpha(texture, inside, inside), Is.GreaterThan(0.9f),
                "corner arc cut too far");
            Assert.That(Alpha(texture, w - 1 - inside, inside), Is.GreaterThan(0.9f));
            Assert.That(Alpha(texture, inside, h - 1 - inside), Is.GreaterThan(0.9f));

            // The middle of every side is solid, all the way to the edge.
            Assert.That(Alpha(texture, w / 2, 1), Is.GreaterThan(0.9f), "top edge is not flat");
            Assert.That(Alpha(texture, w / 2, h - 2), Is.GreaterThan(0.9f), "bottom edge is not flat");
            Assert.That(Alpha(texture, 1, h / 2), Is.GreaterThan(0.9f), "left edge is not flat");
            Assert.That(Alpha(texture, w - 2, h / 2), Is.GreaterThan(0.9f), "right edge is not flat");
            Assert.That(Alpha(texture, w / 2, h / 2), Is.GreaterThan(0.9f), "centre is not solid");
        }

        [Test]
        public void RoundedSprite_MatchesTheWidgetSoThereIsNothingLeftToStretch()
        {
            var sprite = Rounded(140, 56);

            Assert.That(sprite.texture.width, Is.EqualTo(140),
                "a sprite narrower than its widget is a sprite the renderer has to scale");
            Assert.That(sprite.texture.height, Is.EqualTo(56));

            // No border and no nine-slice: with the sprite sized to the widget, a plain stretched
            // image is the whole mechanism, and a border would invite the scaling that broke it.
            Assert.That(sprite.border, Is.EqualTo(Vector4.zero),
                "a border here can only mean the sprite is meant to be sliced");
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(1f),
                "one unit per pixel is what keeps the corner at the units it was drawn in");
        }

        [Test]
        public void RoundedSprite_KeepsTheAspectOfAWidgetTooBigToFitItsTexture()
        {
            // Above the cap the texture is generated smaller, which is only harmless while the
            // aspect survives: a squashed texture would stretch the corner into an ellipse all
            // over again, which is exactly the failure this sprite was rewritten to escape.
            var sprite = Rounded(820, 700);
            float wanted = 820f / 700f;
            float made = (float)sprite.texture.width / sprite.texture.height;
            Assert.That(made, Is.EqualTo(wanted).Within(0.01f),
                "the generated texture is " + made.ToString("0.000") + ":1, the widget is " +
                wanted.ToString("0.000") + ":1");
        }

        [Test]
        public void RoundedSprite_IsSharedByEveryWidgetOfThatSize()
        {
            var first = Rounded(140, 56);
            var again = Rounded(140, 56);
            var other = Rounded(120, 56);

            Assert.That(again, Is.SameAs(first), "the same size generated a second texture");
            Assert.That(other, Is.Not.SameAs(first),
                "different sizes were given one sprite, so at least one is stretched");
        }

        /// <summary>Writes the alpha map so the shape can be looked at, not only asserted.</summary>
        [Test]
        public void RoundedSprite_WritesItsAlphaMapForInspection()
        {
            var texture = Rounded(84, 84).texture;
            var builder = new StringBuilder();
            builder.AppendLine("UiFactory.RoundedSprite(84, 84) alpha (alpha > 0.5 => '#'), "
                + "origin bottom-left, " + texture.width + "x" + texture.height);

            for (int y = texture.height - 1; y >= 0; y--)
            {
                for (int x = 0; x < texture.width; x++)
                    builder.Append(Alpha(texture, x, y) > 0.5f ? '#' : '.');
                builder.AppendLine();
            }

            var logsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "logs"));
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "rounded-sprite.txt"), builder.ToString(),
                new UTF8Encoding(false));
        }
    }
}

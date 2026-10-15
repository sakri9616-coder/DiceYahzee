using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ParvaMirabilia.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParvaMirabilia.Tests.PlayMode
{
    /// <summary>
    /// The sprite a die is actually handed at runtime.
    ///
    /// The screenshot came back with dice that are perfect circles and buttons that are
    /// ellipses, while the sprite itself - checked in <c>SpriteSkinTests</c> - is a rounded
    /// rectangle. Two investigations had to be told apart: is the die holding the wrong sprite,
    /// or is the renderer distorting the right one? So this test looks at the component the
    /// camera draws - which sprite it holds, what shape that sprite is, and whether it was cut
    /// to the die's own size - because a sprite that matches its widget has no scaling left to
    /// turn a corner into half the rectangle.
    /// </summary>
    public sealed class DiceShapeTests
    {
        private GameFlow _flow;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameFlow.ConfigOverride = null;
            InputLog.Close();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (flow != null)
                UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DieFace_HoldsASpriteCutToItsOwnSize()
        {
            StartDuel();
            yield return null;

            var face = DieComponent(0, "_face");
            Assert.That(face.type, Is.EqualTo(Image.Type.Simple),
                "the die is still nine-sliced, and slicing scales the corner with the widget");
            Assert.That(face.sprite, Is.Not.Null, "the face has no sprite at all");

            Assert.That(face.sprite.border, Is.EqualTo(Vector4.zero),
                "the face is drawing a bordered sprite - that is what turned dice into circles");
            Assert.That(face.sprite.pixelsPerUnit, Is.EqualTo(1f),
                "pixels-per-unit of one is what keeps the corner at the units it was drawn in");

            // The whole point of generating the sprite at the widget's size: one pixel per unit,
            // so there is no scaling left to turn a corner into half the rectangle.
            Vector2 size = face.rectTransform.rect.size;
            Texture2D texture = face.sprite.texture;
            Assert.That(texture.width, Is.EqualTo(Mathf.RoundToInt(size.x)),
                "the sprite is " + texture.width + "px wide on a " + size.x + "-unit die");
            Assert.That(texture.height, Is.EqualTo(Mathf.RoundToInt(size.y)));

            Assert.That(texture.GetPixel(0, 0).a, Is.LessThan(0.1f),
                "the sprite on the die has a solid corner, so it is not the rounded one");
            Assert.That(texture.GetPixel(8, 8).a, Is.GreaterThan(0.9f),
                "the sprite on the die has a hollow corner, so it is a circle");
            Assert.That(texture.GetPixel(texture.width / 2, 1).a, Is.GreaterThan(0.9f),
                "the sprite on the die has a hollow top edge, so it is a circle");
        }

        [UnityTest]
        public IEnumerator RingAndFace_AreNotSharingASpriteWithThePips()
        {
            StartDuel();
            yield return null;

            var face = DieComponent(0, "_face");
            var ring = DieComponent(0, "_ring");
            var pip = DieComponent(0, "_pips");
            Assert.That(face.sprite, Is.Not.SameAs(pip.sprite),
                "the face and the pips are the same sprite, which is why the die comes out round");
            Assert.That(ring.sprite, Is.Not.SameAs(pip.sprite),
                "the ring and the pips are the same sprite");

            Vector2 ringSize = ring.rectTransform.rect.size;
            Assert.That(ring.sprite.texture.width, Is.EqualTo(Mathf.RoundToInt(ringSize.x)),
                "the ring's sprite is not cut to the ring either");
        }

        // ------------------------------------------------------------------ helpers
        private Image DieComponent(int index, string field)
        {
            var list = typeof(GameFlow).GetField("_playerDice",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_flow) as System.Collections.IList;
            Assert.That(list, Is.Not.Null, "_playerDice is missing");
            Assert.That(list.Count, Is.GreaterThan(index), "there is no die " + index);

            object view = list[index];
            FieldInfo fieldInfo = view.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fieldInfo, Is.Not.Null, field + " is missing on the die");

            object component = fieldInfo.GetValue(view);
            if (component is Array array && array.Length > 0)
                component = array.GetValue(0);

            var image = component as Image;
            Assert.That(image, Is.Not.Null, field + " is not an Image");
            return image;
        }

        private void StartDuel()
        {
            PlayerPrefs.SetInt("pv.audioStep", 3);
            PlayerPrefs.Save();

            _flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (_flow == null)
            {
                var host = new GameObject("GameFlowUnderTest");
                _flow = host.AddComponent<GameFlow>();
            }

            GameFlow.ConfigOverride = null;
            typeof(GameFlow).GetMethod("StartMatch",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_flow, null);
        }
    }
}

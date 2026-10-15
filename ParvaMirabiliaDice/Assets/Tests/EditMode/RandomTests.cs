using System;
using NUnit.Framework;
using ParvaMirabilia.Core;

namespace ParvaMirabilia.Tests
{
    public class RandomTests
    {
        [Test]
        public void SameSeed_ProducesTheSameSequence()
        {
            var a = new SeededRandom(12345);
            var b = new SeededRandom(12345);

            for (int i = 0; i < 1000; i++)
                Assert.That(a.Range(1, 7), Is.EqualTo(b.Range(1, 7)));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int differences = 0;

            for (int i = 0; i < 100; i++)
            {
                if (a.Range(1, 7) != b.Range(1, 7))
                    differences++;
            }

            Assert.That(differences, Is.GreaterThan(0));
        }

        [Test]
        public void Range_StaysInsideItsBounds()
        {
            var rng = new SeededRandom(999);
            for (int i = 0; i < 20000; i++)
            {
                int value = rng.Range(1, 7);
                Assert.That(value, Is.InRange(1, 6));
            }
        }

        [Test]
        public void Range_CoversEveryFace()
        {
            var rng = new SeededRandom(7);
            var seen = new bool[7];
            for (int i = 0; i < 5000; i++)
                seen[rng.Range(1, 7)] = true;

            for (int face = 1; face <= 6; face++)
                Assert.That(seen[face], Is.True, "face " + face + " never appeared");
        }

        [Test]
        public void Range_RejectsAnEmptyInterval()
        {
            var rng = new SeededRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Range(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Range(5, 2));
        }

        [Test]
        public void NextDouble_StaysInRange()
        {
            var rng = new SeededRandom(4242);
            for (int i = 0; i < 5000; i++)
            {
                double value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }
    }
}

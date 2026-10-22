using System;
using System.Collections;
using UnityEngine;

namespace DiceAndScull
{
    public interface IRevealEffect
    {
        IEnumerator Animate(Transform card, GameObject symbol, CardKind kind);
    }

    public sealed class SnapRevealEffect : IRevealEffect
    {
        public IEnumerator Animate(Transform card, GameObject symbol, CardKind kind)
        {
            Vector3 original = card.localScale;
            float elapsed = 0;
            while (elapsed < 0.11f)
            {
                elapsed += Time.deltaTime;
                card.localScale = new Vector3(original.x * Mathf.Max(0.04f, 1f - elapsed / 0.11f), original.y, original.z);
                yield return null;
            }
            symbol.SetActive(true);
            card.GetComponent<Renderer>().material.color = kind == CardKind.Flower
                ? new Color(0.98f, 0.89f, 0.75f) : new Color(0.31f, 0.11f, 0.17f);
            elapsed = 0;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.15f);
                card.localScale = new Vector3(original.x * (0.04f + 1.13f * t), original.y, original.z);
                yield return null;
            }
            card.localScale = original;
        }
    }

    public sealed class TablePresentation : MonoBehaviour
    {
        private GameObject[,] cards;
        private GameObject[,] symbols;
        private Transform cardRoot;
        private IRevealEffect effect = new SnapRevealEffect();

        public void SetEffect(IRevealEffect replacement)
        {
            effect = replacement ?? throw new ArgumentNullException(nameof(replacement));
        }

        public static Vector3 SeatPosition(int seat)
        {
            switch (seat)
            {
                case 0: return new Vector3(0, 0, -3.3f);
                case 1: return new Vector3(-4.8f, 0, 0);
                case 2: return new Vector3(0, 0, 3.3f);
                case 3: return new Vector3(4.8f, 0, 0);
                default: throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        public void Place(Board board)
        {
            Clear();
            int count = board.Seats[0].Length;
            cards = new GameObject[4, count];
            symbols = new GameObject[4, count];
            cardRoot = new GameObject("Cards").transform;
            cardRoot.SetParent(transform, false);
            for (int seat = 0; seat < 4; seat++)
            for (int index = 0; index < count; index++)
            {
                float offset = (index - (count - 1) / 2f) * 0.68f;
                Vector3 position = SeatPosition(seat) + (seat % 2 == 0 ? new Vector3(offset, 0.22f, 0) : new Vector3(0, 0.22f, offset));
                var card = GameView.Shape(PrimitiveType.Cube, "Card " + seat + ":" + index, position,
                    seat % 2 == 0 ? new Vector3(0.55f, 0.08f, 0.80f) : new Vector3(0.80f, 0.08f, 0.55f),
                    new Color(0.12f, 0.16f, 0.19f));
                card.transform.SetParent(cardRoot, true);
                cards[seat, index] = card;
                symbols[seat, index] = CreateSymbol(card.transform, board.Seats[seat][index]);
                symbols[seat, index].SetActive(false);
            }
        }

        public void Play(RoundResult round, Action<int, int> progress, Action finished)
        {
            if (cards == null) throw new InvalidOperationException("Place a board first.");
            StartCoroutine(Show(round, progress, finished));
        }

        private IEnumerator Show(RoundResult round, Action<int, int> progress, Action finished)
        {
            // Rules calculate both outcomes from one immutable board before presentation begins.
            int steps = Math.Max(round.PlayerReveals.Length, round.EnvironmentReveals.Length);
            var seen = new bool[4, cards.GetLength(1)];
            int playerScore = 0;
            int environmentScore = 0;
            yield return new WaitForSeconds(0.27f);
            for (int i = 0; i < steps; i++)
            {
                if (i < round.PlayerReveals.Length)
                {
                    Reveal reveal = round.PlayerReveals[i];
                    playerScore = reveal.RunningScore;
                    if (!seen[reveal.Seat, reveal.Index])
                    {
                        seen[reveal.Seat, reveal.Index] = true;
                        StartCoroutine(effect.Animate(cards[reveal.Seat, reveal.Index].transform,
                            symbols[reveal.Seat, reveal.Index], reveal.Card));
                    }
                }
                if (i < round.EnvironmentReveals.Length)
                {
                    Reveal reveal = round.EnvironmentReveals[i];
                    environmentScore = reveal.RunningScore;
                    if (!seen[reveal.Seat, reveal.Index])
                    {
                        seen[reveal.Seat, reveal.Index] = true;
                        StartCoroutine(effect.Animate(cards[reveal.Seat, reveal.Index].transform,
                            symbols[reveal.Seat, reveal.Index], reveal.Card));
                    }
                }
                progress?.Invoke(playerScore, environmentScore);
                yield return new WaitForSeconds(0.27f);
            }
            yield return new WaitForSeconds(0.55f);
            Clear();
            finished?.Invoke();
        }

        public void Clear()
        {
            if (cardRoot != null) Destroy(cardRoot.gameObject);
            cardRoot = null;
            cards = null;
            symbols = null;
        }

        private static GameObject CreateSymbol(Transform parent, CardKind kind)
        {
            var symbol = new GameObject(kind.ToString());
            symbol.transform.SetParent(parent, false);
            if (kind == CardKind.Flower)
            {
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2 / 5;
                    var petal = GameView.Shape(PrimitiveType.Sphere, "Petal", Vector3.zero,
                        new Vector3(0.19f, 0.055f, 0.19f), new Color(0.96f, 0.35f, 0.47f));
                    petal.transform.SetParent(symbol.transform, false);
                    petal.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.17f, 0.6f, Mathf.Sin(angle) * 0.17f);
                }
                var center = GameView.Shape(PrimitiveType.Sphere, "Center", Vector3.zero,
                    new Vector3(0.17f, 0.07f, 0.17f), new Color(0.99f, 0.70f, 0.29f));
                center.transform.SetParent(symbol.transform, false);
                center.transform.localPosition = new Vector3(0, 0.65f, 0);
            }
            else
            {
                var bone = GameView.Shape(PrimitiveType.Sphere, "Skull", Vector3.zero,
                    new Vector3(0.48f, 0.07f, 0.38f), new Color(0.96f, 0.88f, 0.76f));
                bone.transform.SetParent(symbol.transform, false);
                bone.transform.localPosition = new Vector3(0, 0.57f, 0);
                for (int i = -1; i <= 1; i += 2)
                {
                    var eye = GameView.Shape(PrimitiveType.Sphere, "Eye", Vector3.zero,
                        new Vector3(0.09f, 0.04f, 0.09f), new Color(0.08f, 0.10f, 0.12f));
                    eye.transform.SetParent(symbol.transform, false);
                    eye.transform.localPosition = new Vector3(i * 0.11f, 0.72f, -0.03f);
                }
            }
            return symbol;
        }
    }
}

using System;
using System.Collections.Generic;

namespace ParvaMirabilia.Core
{
    public enum DuelBlessing { Ember, Hearth, Constellation, Reservoir, Spark, Shelter, Vitality, Mirror, Crown }
    public enum DuelRunPhase { ChoosingBlessing, Fighting, Cleared, Failed }

    /// <summary>Three duels, persistent life, and three deliberate build choices. No metagame grind.</summary>
    public sealed class DuelRun
    {
        private readonly IRandomSource _random;
        private readonly List<DuelBlessing> _blessings = new List<DuelBlessing>();
        private readonly GameConfig _build = new GameConfig().ApplyTacticalDuelLayer();
        private DuelBlessing[] _offers;
        private MatchEngine _currentMatch;
        public int Fight { get; private set; } = 1;
        public int Life { get; private set; } = 32;
        public int MaxLife => _build.StartingHp;
        public int TotalDamage { get; private set; }
        public int TotalRounds { get; private set; }
        public DuelRunPhase Phase { get; private set; } = DuelRunPhase.ChoosingBlessing;
        public IReadOnlyList<DuelBlessing> Blessings => _blessings;
        public IReadOnlyList<DuelBlessing> Offers => _offers;
        public NpcArchetype Opponent => Fight == 1 ? NpcArchetype.Aggressive :
            Fight == 2 ? NpcArchetype.Guardian : NpcArchetype.Balanced;
        public int OpponentLife => 28 + (Fight - 1) * 6;

        public DuelRun(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _build.NpcMaxMana = _build.MaxMana;
            _build.NpcStartingMana = _build.StartingMana;
            _build.NpcEmpowerAttack = _build.EmpowerAttack;
            DrawBlessings();
        }

        private void DrawBlessings()
        {
            var candidates = new List<DuelBlessing>();
            foreach (DuelBlessing blessing in Enum.GetValues(typeof(DuelBlessing)))
                if (!_blessings.Contains(blessing)) candidates.Add(blessing);
            _offers = new DuelBlessing[3];
            for (int i = 0; i < 3; i++)
            {
                int index = _random.Range(0, candidates.Count);
                _offers[i] = candidates[index];
                candidates.RemoveAt(index);
            }
        }

        public void ChooseBlessing(int index)
        {
            if (Phase != DuelRunPhase.ChoosingBlessing || index < 0 || index >= _offers.Length)
                throw new InvalidOperationException("今はその祝福を選べません");
            var blessing = _offers[index];
            _blessings.Add(blessing);
            switch (blessing)
            {
                case DuelBlessing.Ember: _build.PlayerSpellAttackBonus[(int)YakuKind.Pair] += 4; break;
                case DuelBlessing.Hearth: _build.PlayerSpellAttackBonus[(int)YakuKind.Triple] += 5; break;
                case DuelBlessing.Constellation: _build.PlayerSpellAttackBonus[(int)YakuKind.SmallRun] += 5; break;
                case DuelBlessing.Reservoir: _build.MaxMana++; _build.StartingMana++; break;
                case DuelBlessing.Spark: _build.EmpowerAttack += 3; break;
                case DuelBlessing.Shelter: _build.PlayerSpellWardBonus[(int)YakuKind.Chance] += 3; break;
                case DuelBlessing.Vitality: _build.StartingHp += 10; Life += 10; break;
                case DuelBlessing.Mirror: _build.PlayerSpellWardBonus[(int)YakuKind.LargeRun] += 8; break;
                case DuelBlessing.Crown:
                    _build.PlayerSpellAttackBonus[(int)YakuKind.Pair] += 2;
                    _build.PlayerSpellWardBonus[(int)YakuKind.Pair] += 2; break;
            }
            Phase = DuelRunPhase.Fighting;
            _offers = Array.Empty<DuelBlessing>();
        }

        public GameConfig FightConfig()
        {
            if (Phase != DuelRunPhase.Fighting) throw new InvalidOperationException("先に祝福を選んでください");
            var config = _build.Clone();
            config.PlayerOpeningHp = Life;
            config.NpcStartingHp = OpponentLife;
            return config;
        }

        public MatchEngine BeginFight(IRandomSource diceRandom)
        {
            if (_currentMatch != null) throw new InvalidOperationException("この決闘はすでに始まっています");
            _currentMatch = new MatchEngine(FightConfig(), diceRandom, Opponent);
            return _currentMatch;
        }

        public void CompleteFight(MatchEngine game)
        {
            if (Phase != DuelRunPhase.Fighting || !ReferenceEquals(game, _currentMatch) || game.Outcome == null)
                throw new InvalidOperationException("試練の決闘が終わっていません");
            _currentMatch = null;
            TotalDamage += game.PlayerDamageDealt;
            TotalRounds += game.Round;
            Life = Math.Max(0, game.PlayerHp);
            if (game.Outcome.Result != MatchResult.PlayerWin) { Phase = DuelRunPhase.Failed; return; }
            if (Fight == 3) { Phase = DuelRunPhase.Cleared; return; }
            Fight++;
            Life = Math.Min(MaxLife, Life + 16);
            Phase = DuelRunPhase.ChoosingBlessing;
            DrawBlessings();
        }

        public static string Name(DuelBlessing blessing)
        {
            switch (blessing)
            {
                case DuelBlessing.Ember: return "双火の印";
                case DuelBlessing.Hearth: return "三火の炉";
                case DuelBlessing.Constellation: return "星の連なり";
                case DuelBlessing.Reservoir: return "魔力の器";
                case DuelBlessing.Spark: return "火花の増幅";
                case DuelBlessing.Shelter: return "旅人の守り";
                case DuelBlessing.Vitality: return "生命の灯";
                case DuelBlessing.Mirror: return "星鏡の障壁";
                default: return "双璧の王冠";
            }
        }

        public static string Description(DuelBlessing blessing)
        {
            switch (blessing)
            {
                case DuelBlessing.Ember: return "ペアの攻撃 ＋4\n低いペアにも、攻める価値を。";
                case DuelBlessing.Hearth: return "スリーカインドの攻撃 ＋5\n同じ目を集めて、一撃を重く。";
                case DuelBlessing.Constellation: return "小ストレートの攻撃 ＋5\n並んだ目を、守りを破る力に。";
                case DuelBlessing.Reservoir: return "魔力の上限と決闘開始時の魔力 ＋1\n早めに止めて、次の強化を蓄える。";
                case DuelBlessing.Spark: return "魔力による攻撃強化 ＋3\n同じ魔力1で、より強い一撃。";
                case DuelBlessing.Shelter: return "チャンスの守り ＋3\n休まない役で、被弾を抑える。";
                case DuelBlessing.Vitality: return "最大HPと現在HP ＋10\n次の決闘まで、余裕を残す。";
                case DuelBlessing.Mirror: return "大ストレートの守り ＋8\n攻める役にも、防壁を持たせる。";
                default: return "ペアの攻撃・守り ＋2\n小さな役を、攻守の要に。";
            }
        }
    }
}

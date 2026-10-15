"""Design study only. Does not write game code or change scoring.

Enumerates every fair 5d6 outcome (252 unordered states weighted to 7,776
ordered outcomes). Exact expected-score planning for up to two rerolls,
all nine categories available; no opponent, HP, save, or future card value.
"""
import itertools
import json
import math
from pathlib import Path

NAMES = ['Pair', 'TwoPair', 'Three', 'FullHouse', 'SmallStraight', 'Four', 'LargeStraight', 'Five', 'Chance']
BONUSES = [4, 8, 12, 18, 18, 24, 26, 40, 0]
MULTIPLIERS = [1.2, 1.4, 1.7, 2, 2, 2.4, 2.5, 3.3, 1]
STATES = list(itertools.combinations_with_replacement(range(1, 7), 5))

def formed(dice):
    counts = [dice.count(i) for i in range(1, 7)]
    distinct = set(dice)
    return [max(counts) >= 2, sum(c >= 2 for c in counts) >= 2,
            max(counts) >= 3, 3 in counts and 2 in counts,
            any(set(range(i, i + 4)) <= distinct for i in range(1, 4)),
            max(counts) >= 4, any(set(range(i, i + 5)) <= distinct for i in range(1, 3)),
            max(counts) == 5, True]

def scores(dice, mode):
    total = sum(dice)
    flags = formed(dice)
    if mode == 'current_additive':
        return [total + (bonus if yes else 0) for bonus, yes in zip(BONUSES, flags)]
    if mode == 'raw_multiplier':
        return [total * (m if yes else 1) for m, yes in zip(MULTIPLIERS, flags)]
    # Alternative base reduces the ratio between minimum and maximum faces.
    # Candidate only; whole duel damage scale would need recalibration.
    return [(20 + total) * (m if yes else 1) for m, yes in zip(MULTIPLIERS, flags)]

def weight(dice):
    return math.factorial(len(dice)) // math.prod(math.factorial(dice.count(i)) for i in range(1, 7))

def held_options(dice):
    return sorted(set(tuple(dice[i] for i in range(5) if mask & (1 << i)) for mask in range(32)),
                  key=lambda h: (-len(h), h))

OPTIONS = {state: held_options(state) for state in STATES}
PARTIALS = sorted(set(h for options in OPTIONS.values() for h in options))
TRANSITIONS = {}
for held in PARTIALS:
    k = 5 - len(held)
    TRANSITIONS[held] = [(tuple(sorted(held + roll)), weight(roll) / 6 ** k)
                         for roll in itertools.combinations_with_replacement(range(1, 7), k)]

def study(mode):
    immediate = {s: max(scores(s, mode)) for s in STATES}
    values = immediate.copy()
    policies = []
    layers = [values]
    for _ in range(2):
        expected = {h: sum(p * values[s] for s, p in outcomes) for h, outcomes in TRANSITIONS.items()}
        policy = {s: max(OPTIONS[s], key=lambda h: expected[h]) for s in STATES}
        values = {s: expected[policy[s]] for s in STATES}
        layers.append(values); policies.append(policy)
    fixtures = [(1, 1, 1, 1, 1), (6, 6, 6, 6, 6), (1, 1, 2, 2, 2),
                (1, 2, 3, 4, 5), (4, 5, 5, 6, 6), (1, 1, 3, 5, 6)]
    return {
        'mean_score_after_optimal_two_rerolls': sum(weight(s) * values[s] for s in STATES) / 7776,
        'initial_roll_stop_probability': sum(weight(s) for s in STATES if len(policies[1][s]) == 5) / 7776,
        'fixtures': [{'dice': s, 'best_immediate': immediate[s],
                      'best_role': NAMES[max(range(9), key=lambda i: scores(s, mode)[i])],
                      'one_reroll_hold': policies[0][s], 'one_reroll_expected': layers[1][s],
                      'two_reroll_hold': policies[1][s], 'two_reroll_expected': values[s]}
                     for s in fixtures]
    }

assert len(STATES) == 252 and sum(map(weight, STATES)) == 7776
assert formed((1, 1, 1, 1, 1))[3] is False
assert scores((1, 1, 1, 1, 1), 'current_additive')[7] == 45
assert scores((6, 6, 6, 6, 6), 'current_additive')[7] == 70
result = {
    'scope': 'Design hypothesis; fair dice, exact enumeration, expected-score objective only, all categories available. Not a duel win-rate or fun measurement.',
    'multipliers': dict(zip(NAMES, MULTIPLIERS)),
    'ordered_outcomes': 7776, 'unordered_states': len(STATES),
    'modes': {mode: study(mode) for mode in ['current_additive', 'raw_multiplier', 'offset_multiplier']}
}
output = Path(__file__).resolve().parents[1] / 'logs' / 'role-multiplier-study.json'
output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))

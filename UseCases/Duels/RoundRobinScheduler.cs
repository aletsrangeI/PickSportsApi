using Domain.Entities;

namespace UseCases.Duels;

/// <summary>Resultado del sorteo: fixtures (sin Id ni QuinielaId) y la estructura del calendario.</summary>
public record DuelSchedule(
    List<DuelFixture> Fixtures,
    int RoundsPerLeg,
    int Legs,
    List<int> DuelWeeks,
    List<int> RestWeeks);

/// <summary>
/// SPEC-013 Etapa 2: sorteo round-robin (metodo del circulo) determinista para una semilla.
/// Impar = un Bye por vuelta; vueltas completas que caben en la temporada; descansos repartidos
/// de forma pareja sin tocar nunca la ultima jornada.
/// </summary>
public static class RoundRobinScheduler
{
    private const int Dummy = -1;

    public static DuelSchedule Generate(IReadOnlyList<int> memberIds, int totalWeeks, int seed)
    {
        if (memberIds == null || memberIds.Count < 2)
            throw new ArgumentException("Se requieren al menos 2 miembros para sortear duelos.", nameof(memberIds));
        if (totalWeeks < 1)
            throw new ArgumentException("totalWeeks debe ser al menos 1.", nameof(totalWeeks));

        int n = memberIds.Count;

        // Sorteo del orden (Fisher-Yates con semilla).
        var order = memberIds.ToList();
        var rng = new Random(seed);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        if (n % 2 == 1) order.Add(Dummy);
        int m = order.Count;
        int roundsPerLeg = m - 1; // n impar: n ; n par: n-1

        // Rondas de la vuelta 1: lista de (home, away) con away == null para Bye.
        var baseRounds = new List<List<(int Home, int? Away)>>();
        var circle = new List<int>(order);
        for (int r = 0; r < roundsPerLeg; r++)
        {
            var pairs = new List<(int Home, int? Away)>();
            for (int i = 0; i < m / 2; i++)
            {
                int a = circle[i];
                int b = circle[m - 1 - i];
                if (a == Dummy) pairs.Add((b, null));
                else if (b == Dummy) pairs.Add((a, null));
                else pairs.Add((a, b));
            }
            baseRounds.Add(pairs);

            // Rotacion: el primero fijo, el ultimo pasa a la posicion 1.
            var last = circle[m - 1];
            circle.RemoveAt(m - 1);
            circle.Insert(1, last);
        }

        int legs;
        int duelDates;
        if (roundsPerLeg <= totalWeeks)
        {
            legs = Math.Max(1, totalWeeks / roundsPerLeg);
            duelDates = legs * roundsPerLeg;
        }
        else
        {
            legs = 1;
            duelDates = totalWeeks;
        }

        int restCount = totalWeeks - duelDates;
        var restSet = new HashSet<int>();
        for (int i = 1; i <= restCount; i++)
        {
            int target = (int)Math.Round(i * (double)totalWeeks / (restCount + 1), MidpointRounding.AwayFromZero);
            int c = Math.Max(1, target);
            while (c < totalWeeks && restSet.Contains(c)) c++;
            if (c >= totalWeeks)
            {
                c = Math.Min(target, totalWeeks - 1);
                while (c > 1 && restSet.Contains(c)) c--;
            }
            restSet.Add(c);
        }

        var restWeeks = restSet.OrderBy(w => w).ToList();
        var duelWeeks = Enumerable.Range(1, totalWeeks).Where(w => !restSet.Contains(w)).ToList();

        var fixtures = new List<DuelFixture>();
        for (int idx = 0; idx < duelWeeks.Count; idx++)
        {
            int leg = idx / roundsPerLeg + 1;
            int roundIdx = idx % roundsPerLeg;
            bool invert = leg % 2 == 0;
            foreach (var (home, away) in baseRounds[roundIdx])
            {
                if (away == null)
                {
                    fixtures.Add(new DuelFixture
                    {
                        WeekNumber = duelWeeks[idx], Round = roundIdx + 1, Leg = leg,
                        HomeMemberId = home, AwayMemberId = null,
                        HomeHits = 0, AwayHits = 0, Result = "BYE"
                    });
                }
                else
                {
                    fixtures.Add(new DuelFixture
                    {
                        WeekNumber = duelWeeks[idx], Round = roundIdx + 1, Leg = leg,
                        HomeMemberId = invert ? away.Value : home,
                        AwayMemberId = invert ? home : away.Value
                    });
                }
            }
        }

        fixtures = fixtures.OrderBy(f => f.WeekNumber).ThenBy(f => f.Round).ToList();
        return new DuelSchedule(fixtures, roundsPerLeg, legs, duelWeeks, restWeeks);
    }
}

using System.Linq;

namespace SimonScreams
{
    abstract class Criterion
    {
        public abstract string Name { get; }
        public abstract bool Check(int[] seq, int[] ryb);
    }

    sealed class Row1Criterion : Criterion
    {
        public override string Name { get { return "If three adjacent colors flashed in clockwise order"; } }
        public override bool Check(int[] seq, int[] ryb) { return Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix + 1] == (seq[ix] + 1) % 6 && seq[ix + 2] == (seq[ix] + 2) % 6); }
    }
    sealed class Row2Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if a color flashed, then an adjacent color, then the first again"; } }
        public override bool Check(int[] seq, int[] ryb) { return Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix + 2] == seq[ix] && (seq[ix + 1] == (seq[ix] + 1) % 6 || seq[ix + 1] == (seq[ix] + 5) % 6)); }
    }
    sealed class Row3Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if at most one color flashed out of red, yellow, and blue"; } }
        public override bool Check(int[] seq, int[] ryb) { return ryb.Count(color => !seq.Contains(color)) >= 2; }
    }
    sealed class Row4Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if there are two colors opposite each other that didn’t flash"; } }
        public override bool Check(int[] seq, int[] ryb) { return Enumerable.Range(0, 3).Any(col => !seq.Contains(col) && !seq.Contains(col + 3)); }
    }
    sealed class Row5Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if two adjacent colors flashed in clockwise order"; } }
        public override bool Check(int[] seq, int[] ryb) { return Enumerable.Range(0, seq.Length - 1).Any(ix => seq[ix + 1] == (seq[ix] + 1) % 6); }
    }
    sealed class Row6Criterion : Criterion
    {
        public override string Name { get { return "Otherwise"; } }
        public override bool Check(int[] seq, int[] ryb) { return true; }
    }
}

using System.Linq;

namespace SimonScreams
{
    abstract class Criterion
    {
        public abstract string Name { get; }
        public abstract bool Check(int[] seq, int[] rgb, int orangle);
    }

    sealed class Col1Criterion : Criterion
    {
        public override string Name { get { return "If three adjacent colors flashed in counter clockwise order"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix + 1] == (seq[ix] + 5) % 6 && seq[ix + 2] == (seq[ix] + 4) % 6); }
    }
    sealed class Col2Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if orange flashed more than twice"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return seq.Count(n => n == orange) > 2; }
    }
    sealed class Col3Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if two adjacent colors didn’t flash"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, 6).Any(color => !seq.Contains(color) && !seq.Contains((color + 1) % 6)); }
    }
    sealed class Col4Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if exactly two colors flashed exactly twice"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return seq.GroupBy(n => n).Where(g => g.Count() == 2).Count() == 2; }
    }
    sealed class Col5Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if the number of colors that flashed is even"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return seq.Distinct().Count() % 2 == 0; }
    }
    sealed class Col6Criterion : Criterion
    {
        public override string Name { get { return "Otherwise"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return true; }
    }
    sealed class Row1Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if two opposite colors didn’t flash"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, 3).Any(col => !seq.Contains(col) && !seq.Contains(col + 3)); }
    }
    sealed class Row2Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if at most one of red, green and blue flashed"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return rgb.Count(color => seq.Contains(color)) <= 1; }
    }
    sealed class Row3Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if three adjacent colors flashed in clockwise order"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix + 1] == (seq[ix] + 1) % 6 && seq[ix + 2] == (seq[ix] + 2) % 6); }
    }
    sealed class Row4Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if any color flashed, then an adjacent color, then the first again"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix + 2] == seq[ix] && (seq[ix + 1] == (seq[ix] + 1) % 6 || seq[ix + 1] == (seq[ix] + 5) % 6)); }
    }
    sealed class Row5Criterion : Criterion
    {
        public override string Name { get { return "Otherwise, if two adjacent colors flashed in clockwise order"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return Enumerable.Range(0, seq.Length - 1).Any(ix => seq[ix + 1] == (seq[ix] + 1) % 6); }
    }
    sealed class Row6Criterion : Criterion
    {
        public override string Name { get { return "Otherwise"; } }
        public override bool Check(int[] seq, int[] rgb, int orange) { return true; }
    }
}

using System;

namespace SimonScreams
{
    sealed class Criterion
    {
        public string Name { get; private set; }
        public Func<int[], bool> Check { get; private set; }
        public int SouvenirCode { get; private set; }
        public Criterion(string name, Func<int[], bool> check, int souvenirCode = 0)
        {
            Name = name;
            Check = check;
            SouvenirCode = souvenirCode;
        }
    }

    sealed class SmallTableCriterion
    {
        public string Name { get; private set; }
        public Func<KMBombInfo, bool> Check { get; private set; }
        public SmallTableCriterion(string name, Func<KMBombInfo, bool> check)
        {
            Name = name;
            Check = check;
        }
    }

    abstract class CriterionGenerator
    {
        public int Probability { get; private set; }
        public CriterionGenerator(int probability) { Probability = probability; }
        public abstract bool RequiresColors { get; }
        public abstract Criterion GetCriterion(SimonColor[] colors, int[] colorIxs);
    }
    sealed class SpecificCriterion : CriterionGenerator
    {
        private readonly Criterion _criterion;
        public SpecificCriterion(int probability, Criterion criterion) : base(probability) { _criterion = criterion; }
        public override bool RequiresColors { get { return false; } }
        public override Criterion GetCriterion(SimonColor[] colors, int[] colorIxs) { return _criterion; }
    }
    sealed class CriterionFromColors : CriterionGenerator
    {
        private readonly Func<SimonColor[], int[], Criterion> _generator;
        public CriterionFromColors(int probability, Func<SimonColor[], int[], Criterion> generator) : base(probability) { _generator = generator; }
        public override bool RequiresColors { get { return true; } }
        public override Criterion GetCriterion(SimonColor[] colors, int[] colorIxs) { return _generator(colors, colorIxs); }
    }
}

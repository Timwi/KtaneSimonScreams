using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using SimonScreams;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Screams
/// Created by Timwi
/// </summary>
public class SimonScreamsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public KMSelectable MainSelectable;
    public KMSelectable[] Buttons;
    public Light[] Lights;
    public Material[] Materials;
    public Transform FlapsParent;
    public Transform KeypadParent;

    public MeshRenderer[] Leds;
    public Material UnlitLed;
    public Material LitLed;
    public TextMesh[] ColorblindIndicators;

    private SimonColor[] _colors;
    private int[][] _sequences;
    private int[][] _expectedInput;
    private int _stage;
    private int _subprogress;
    private bool _isSolved;
    private bool _makeSounds;
    private Coroutine _blinker;

    private const int numStages = 3, minFirstStageLength = 3, maxFirstStageLength = 5, minStageExtra = 1, maxStageExtra = 2;
    private static readonly string _smallTableColumns = "ACDEFH";

    private Criterion[] _rowCriteria;
    private string[][] _largeTable;
    private SimonColor[][] _smallTable;
    private SmallTableCriterion[] _smallTableRowCriteria;
    private int[] _stageIxs;

    private static readonly string[][] _largeTableSeed1 = Ut.NewArray(
        new[] { "FFC", "CEH", "HAF", "ECD", "DDE", "AHA" },
        new[] { "AHF", "DFC", "ECH", "CDE", "FEA", "HAD" },
        new[] { "DED", "ECF", "FHE", "HAA", "AFH", "CDC" },
        new[] { "HCE", "ADA", "CFD", "DHH", "EAC", "FEF" },
        new[] { "CAH", "FHD", "DDA", "AEC", "HCF", "EFE" },
        new[] { "EDA", "HAE", "AEC", "FFF", "CHD", "DCH" });
    private static readonly SimonColor[][] _smallTableSeed1 = Ut.NewArray(
        new[] { SimonColor.Yellow, SimonColor.Orange, SimonColor.Green, SimonColor.Red, SimonColor.Blue, SimonColor.Purple },
        new[] { SimonColor.Purple, SimonColor.Yellow, SimonColor.Red, SimonColor.Blue, SimonColor.Orange, SimonColor.Green },
        new[] { SimonColor.Orange, SimonColor.Green, SimonColor.Blue, SimonColor.Purple, SimonColor.Red, SimonColor.Yellow },
        new[] { SimonColor.Green, SimonColor.Blue, SimonColor.Orange, SimonColor.Yellow, SimonColor.Purple, SimonColor.Red },
        new[] { SimonColor.Red, SimonColor.Purple, SimonColor.Yellow, SimonColor.Orange, SimonColor.Green, SimonColor.Blue },
        new[] { SimonColor.Blue, SimonColor.Red, SimonColor.Purple, SimonColor.Green, SimonColor.Yellow, SimonColor.Orange });

    private static bool matchesPattern(int[] seq, params int[] offsets) { return Enumerable.Range(0, seq.Length - offsets.Length).Any(ix => Enumerable.Range(0, offsets.Length).All(offsetIx => seq[ix + offsetIx + 1] == (seq[ix] + offsets[offsetIx]) % 6)); }
    private static readonly CriterionGenerator[] _allRowCriteria = Ut.NewArray<CriterionGenerator>(
        new SpecificCriterion(50, new Criterion("every color flashed at least once", seq => Enumerable.Range(0, 6).Count(col => !seq.Contains(col)) == 0)),
        new SpecificCriterion(120, new Criterion("three colors, each two apart, flashed in clockwise order", seq => matchesPattern(seq, 2, 4))),
        new SpecificCriterion(220, new Criterion("a color flashed, then an adjacent color, then the first again", seq => matchesPattern(seq, 1, 0) || matchesPattern(seq, 5, 0))),
        new SpecificCriterion(121, new Criterion("three adjacent colors flashed in counter-clockwise order", seq => matchesPattern(seq, 5, 4))),
        new SpecificCriterion(122, new Criterion("three adjacent colors flashed in clockwise order", seq => matchesPattern(seq, 1, 2))),
        new SpecificCriterion(123, new Criterion("a color flashed, then the one opposite, then the first again", seq => matchesPattern(seq, 3, 0))),
        new SpecificCriterion(100, new Criterion("three adjacent colors did not flash", seq => Enumerable.Range(0, 6).Any(col => !seq.Contains(col) && !seq.Contains((col + 1) % 6) && !seq.Contains((col + 2) % 6)))),
        new SpecificCriterion(170, new Criterion("the colors flashing first and last are the same", seq => seq[0] == seq.Last())),
        new SpecificCriterion(124, new Criterion("three colors, each two apart, flashed in counter-clockwise order", seq => matchesPattern(seq, 4, 2))),
        new SpecificCriterion(230, new Criterion("a color flashed, then a color two away, then the first again", seq => matchesPattern(seq, 2, 0) || matchesPattern(seq, 4, 0))),
        new SpecificCriterion(260, new Criterion("a color flashed, then one adjacent, then the one opposite that", seq => matchesPattern(seq, 1, 4) || matchesPattern(seq, 5, 2))),
        new SpecificCriterion(261, new Criterion("a color flashed, then one adjacent, then the one opposite the first", seq => matchesPattern(seq, 1, 3) || matchesPattern(seq, 5, 3))),
        new SpecificCriterion(262, new Criterion("a color flashed, then a color two away, then the one opposite that", seq => matchesPattern(seq, 2, 5) || matchesPattern(seq, 4, 1))),
        new SpecificCriterion(263, new Criterion("a color flashed, then a color two away, then the one opposite the first", seq => matchesPattern(seq, 2, 3) || matchesPattern(seq, 4, 3))),
        new CriterionFromColors(231, (colors, colorIxs) => new Criterion(string.Format("at most one color flashed out of {0}, {1}, and {2}", colors[0], colors[1], colors[2]), seq => colorIxs.Count(cIx => seq.Contains(cIx)) <= 1)),
        new SpecificCriterion(264, new Criterion("a color flashed, then the one opposite, then one adjacent to the first", seq => matchesPattern(seq, 3, 1) || matchesPattern(seq, 3, 5))),
        new SpecificCriterion(221, new Criterion("no color flashed more than once", seq => Enumerable.Range(0, 6).All(col => seq.Count(c => c == col) <= 1))),
        new SpecificCriterion(265, new Criterion("a color flashed, then the one opposite, then one adjacent to that", seq => matchesPattern(seq, 3, 2) || matchesPattern(seq, 3, 4))),
        new SpecificCriterion(240, new Criterion("exactly two colors flashed exactly twice", seq => Enumerable.Range(0, 6).Count(col => seq.Count(c => c == col) == 2) == 2)),
        new SpecificCriterion(420, new Criterion("there are two colors adjacent to each other that didn’t flash", seq => Enumerable.Range(0, 6).Any(col => !seq.Contains(col) && !seq.Contains((col + 1) % 6)))),
        new SpecificCriterion(270, new Criterion("there is exactly one color that didn’t flash", seq => Enumerable.Range(0, 6).Count(col => !seq.Contains(col)) == 1)),
        new SpecificCriterion(280, new Criterion("no color flashed exactly twice", seq => Enumerable.Range(0, 6).All(col => seq.Count(c => c == col) != 2))),
        new SpecificCriterion(290, new Criterion("there are at least three colors that didn’t flash", seq => Enumerable.Range(0, 6).Count(col => !seq.Contains(col)) >= 3)),
        new SpecificCriterion(300, new Criterion("exactly two colors flashed more than once", seq => Enumerable.Range(0, 6).Count(col => seq.Count(c => c == col) > 1) == 2)),
        new SpecificCriterion(330, new Criterion("the colors flashing first and last are adjacent", seq => seq[0] == (seq.Last() + 1) % 6 || seq[0] == (seq.Last() + 5) % 6)),
        new SpecificCriterion(380, new Criterion("exactly one color flashed more than once", seq => Enumerable.Range(0, 6).Count(col => seq.Count(c => c == col) > 1) == 1)),
        new SpecificCriterion(440, new Criterion("there are two colors two away from each other that didn’t flash", seq => Enumerable.Range(0, 6).Any(col => !seq.Contains(col) && !seq.Contains((col + 2) % 6)))),
        new SpecificCriterion(266, new Criterion("there are two colors opposite each other that didn’t flash", seq => Enumerable.Range(0, 3).Any(col => !seq.Contains(col) && !seq.Contains(col + 3)))),
        new SpecificCriterion(400, new Criterion("there are exactly two colors that didn’t flash", seq => Enumerable.Range(0, 6).Count(col => !seq.Contains(col)) == 2)),
        new SpecificCriterion(410, new Criterion("exactly one color flashed exactly twice", seq => Enumerable.Range(0, 6).Count(col => seq.Count(c => c == col) == 2) == 1)),
        new SpecificCriterion(480, new Criterion("the number of distinct colors that flashed is even", seq => Enumerable.Range(0, 6).Count(col => seq.Contains(col)) % 2 == 0)),
        new SpecificCriterion(390, new Criterion("no two adjacent colors flashed in clockwise order", seq => !matchesPattern(seq, 1))),
        new SpecificCriterion(391, new Criterion("no two adjacent colors flashed in counter-clockwise order", seq => !matchesPattern(seq, 5))),
        new SpecificCriterion(610, new Criterion("two adjacent colors flashed in clockwise order", seq => matchesPattern(seq, 1))),
        new SpecificCriterion(392, new Criterion("no two colors two apart flashed in counter-clockwise order", seq => !matchesPattern(seq, 4))),
        new SpecificCriterion(500, new Criterion("the colors flashing first and last are different and not adjacent", seq => seq[0] != (seq.Last() + 1) % 6 && seq[0] != (seq.Last() + 5) % 6 && seq[0] != seq.Last())),
        new SpecificCriterion(520, new Criterion("a color flashed, then another color, then the first", seq => Enumerable.Range(0, seq.Length - 2).Any(ix => seq[ix] == seq[ix + 2]))),
        new SpecificCriterion(393, new Criterion("no two colors two apart flashed in clockwise order", seq => !matchesPattern(seq, 2))),
        new SpecificCriterion(611, new Criterion("two adjacent colors flashed in counter-clockwise order", seq => matchesPattern(seq, 5))),
        new SpecificCriterion(612, new Criterion("two colors two apart flashed in clockwise order", seq => matchesPattern(seq, 2))),
        new SpecificCriterion(613, new Criterion("two colors two apart flashed in counter-clockwise order", seq => matchesPattern(seq, 4))),
        new SpecificCriterion(521, new Criterion("the number of distinct colors that flashed is odd", seq => Enumerable.Range(0, 6).Count(col => seq.Contains(col)) % 2 == 1)),
        new CriterionFromColors(770, (colors, colIxs) => new Criterion(string.Format("at least two colors flashed out of {0}, {1}, and {2}", colors[0], colors[1], colors[2]), seq => colIxs.Count(clrIx => seq.Contains(clrIx)) >= 2)));

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _finishedAnimating

    private static Vector3[] _unrotatedFlapOutline;
    static SimonScreamsModule()
    {
        const float innerRadius = 0.4f;
        const float outerRadius = 1.02f;
        const float cos = 0.866f;
        const float sin = 0.5f;
        const float depth = .01f;
        const float offset = .025f;

        _unrotatedFlapOutline = new[] { new Vector3(offset, -depth, 0), new Vector3(innerRadius * cos + offset, -depth, innerRadius * sin), new Vector3(outerRadius + offset, -depth, 0), new Vector3(innerRadius * cos + offset, -depth, -innerRadius * sin) };
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _isSolved = false;
        _stage = 0;
        _subprogress = 0;
        _colors = ((SimonColor[]) Enum.GetValues(typeof(SimonColor))).Shuffle();
        _sequences = generateSequences();
        _makeSounds = false;

        var colorblind = GetComponent<KMColorblindMode>().ColorblindModeActive;
        for (int i = 0; i < 6; i++)
        {
            ColorblindIndicators[i].text = _colors[i].ToString().ToUpperInvariant();
            ColorblindIndicators[i].gameObject.SetActive(colorblind);

            var mat = Materials[(int) _colors[i]];
            Buttons[i].GetComponent<MeshRenderer>().material = mat;
            Lights[i].color = mat.color;
            Buttons[i].OnInteract = HandlePress(i);
        }

        for (int i = 0; i < 3; i++)
            Leds[i].material = UnlitLed;

        Debug.LogFormat("[Simon Screams #{1}] Colors in clockwise order are: {0}", _colors.JoinString(", "), _moduleId);

        /* RULE SEED */
        /* (relies on _colors already being decided) */

        var rnd = RuleSeedable.GetRNG();
        var steps = rnd.Next(0, 25);
        for (var i = 0; i < steps; i++)
            rnd.NextDouble();

        var rules = rnd.ShuffleFisherYates(_allRowCriteria.ToArray()).Take(5).ToArray();
        Array.Sort(rules, (a, b) => a.Probability.CompareTo(b.Probability));
        _rowCriteria = new Criterion[6];
        for (int i = 0; i < 5; i++)
        {
            if (rules[i].RequiresColors)
            {
                var colors = rnd.ShuffleFisherYates(new[] { SimonColor.Orange, SimonColor.Yellow, SimonColor.Red, SimonColor.Green, SimonColor.Blue, SimonColor.Purple });
                _rowCriteria[i] = rules[i].GetCriterion(colors, colors.Take(3).Select(c => Array.IndexOf(_colors, c)).ToArray());
            }
            else
                _rowCriteria[i] = rules[i].GetCriterion(null, null);
        }
        _rowCriteria[5] = new Criterion("Otherwise", seq => true);

        var gt = rnd.Next(0, 2) != 0;
        var ch = gt ? '≥' : '≤';
        Func<int, bool> cmp = n => gt ? (n >= 3) : (n <= 3);

        _smallTableRowCriteria = rnd.ShuffleFisherYates(Ut.NewArray(
            new SmallTableCriterion(ch + " 3 ports", b => cmp(b.GetPortCount())),
            new SmallTableCriterion(ch + " 3 indicators", b => cmp(b.GetIndicators().Count())),
            new SmallTableCriterion(ch + " 3 batteries", b => cmp(b.GetBatteryCount())),
            new SmallTableCriterion(ch + " 3 digits in serial number", b => cmp(b.GetSerialNumberNumbers().Count())),
            new SmallTableCriterion(ch + " 3 letters in serial number", b => cmp(b.GetSerialNumberLetters().Count())),
            new SmallTableCriterion(ch + " 3 battery holders", b => cmp(b.GetBatteryHolderCount()))));

        if (rnd.Seed == 1)
        {
            _largeTable = _largeTableSeed1;
            _smallTable = _smallTableSeed1;
            _stageIxs = new[] { 0, 1, 2 };
        }
        else
        {
            _stageIxs = new int[3];
            _stageIxs[0] = rnd.Next(0, 3);
            _stageIxs[1] = rnd.Next(_stageIxs[0] + 1, 4);
            _stageIxs[2] = rnd.Next(_stageIxs[1] + 1, 5);

            var numbers = Enumerable.Range(0, 6).ToArray();

            var columnShuffle1 = rnd.ShuffleFisherYates(numbers.ToArray());
            var columnShuffle2 = rnd.ShuffleFisherYates(numbers.ToArray());
            var columnShuffle3 = rnd.ShuffleFisherYates(numbers.ToArray());
            var columnShuffle = new[] { columnShuffle1, columnShuffle2, columnShuffle3 };

            var rowShuffle1 = rnd.ShuffleFisherYates(numbers.ToArray());
            var rowShuffle2 = rnd.ShuffleFisherYates(numbers.ToArray());
            var rowShuffle3 = rnd.ShuffleFisherYates(numbers);
            var rowShuffle = new[] { rowShuffle1, rowShuffle2, rowShuffle3 };

            _largeTable = _largeTableSeed1.Select(row => new string[row.Length]).ToArray();
            for (var r = 0; r < 6; r++)
                for (var c = 0; c < 6; c++)
                    _largeTable[r][c] = Enumerable.Range(0, 3).Select(ix => _largeTableSeed1[rowShuffle[ix][r]][columnShuffle[ix][c]].Substring(ix, 1)).JoinString();

            rnd.ShuffleFisherYates(columnShuffle1);
            rnd.ShuffleFisherYates(rowShuffle1);

            _smallTable = _smallTableSeed1.Select(row => new SimonColor[row.Length]).ToArray();
            for (var r = 0; r < 6; r++)
                _smallTable[r] = Enumerable.Range(0, 6).Select(c => _smallTableSeed1[rowShuffle1[r]][columnShuffle1[c]]).ToArray();
        }

        for (int i = 0; i < 6; i++)
            Debug.LogFormat("<Simon Screams #{0}> Large Table Row {1} = {2}", _moduleId, i + 1, _rowCriteria[i].Name);

        for (int i = 0; i < 6; i++)
            Debug.LogFormat("<Simon Screams #{0}> Small Table Row {1} = {2}", _moduleId, i + 1, _smallTableRowCriteria[i].Name);

        /* END RULE SEED */

        startBlinker(1.5f);
        alignFlaps(0, 90, .01f);
        Bomb.OnBombExploded = delegate { StopAllCoroutines(); };

        float scalar = transform.lossyScale.x;
        foreach (var light in Lights)
            light.range *= scalar;

        var smallTableApplicable = _smallTableRowCriteria.Select(cr => cr.Check(Bomb)).ToArray();
        Debug.LogFormat("[Simon Screams #{1}] Small table applicable rows are: {0}", Enumerable.Range(0, _smallTableRowCriteria.Length).Where(ix => smallTableApplicable[ix]).Select(ix => _smallTableRowCriteria[ix].Name).JoinString(", "), _moduleId);
        _expectedInput = _sequences.Select((seq, stage) =>
        {
            var applicableColumn = _colors[seq[_stageIxs[stage]]];
            var applicableRow = _rowCriteria.IndexOf(cri => cri.Check(seq));
            var smallTableColumn = _smallTableColumns.IndexOf(_largeTable[applicableRow][(int) applicableColumn][stage]);
            return _smallTable.Where((row, ix) => smallTableApplicable[ix]).Select(row => _colors.IndexOf(row[smallTableColumn])).ToArray();
        }).ToArray();

        logCurrentStage();
    }

    private void alignFlaps(int firstBtnIx, float angle, float duration, bool animation = false)
    {
        if (animation)
            StartCoroutine(raiseFlapsParent());
        for (int i = 0; i < 6; i++)
        {
            var btnIx = (i + firstBtnIx) % 6;
            var flapOutline = _unrotatedFlapOutline.Select(p => Quaternion.Euler(0, -(60 * btnIx - 15), 0) * p).ToArray();
            for (int flapIx = 0; flapIx < 4; flapIx++)
                StartCoroutine(rotateFlap(flapOutline, flapIx, btnIx, angle, duration, animation ? 1f + i * .3f : (float?) null));
            if (animation)
                StartCoroutine(lowerButton((i + firstBtnIx) % 6, .5f + i * .3f));
        }
    }

    private IEnumerator raiseFlapsParent()
    {
        yield return new WaitForSeconds(.3f);
        const float duration = 1.5f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            FlapsParent.localPosition = new Vector3(0, 0.009f + .007f * Mathf.Min(1, elapsed / duration), 0);
        }
    }

    private IEnumerator lowerButton(int btnIx, float delay)
    {
        yield return new WaitForSeconds(delay);
        ColorblindIndicators[btnIx].gameObject.SetActive(false);
        const float duration = 1.5f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            Buttons[btnIx].transform.localPosition = new Vector3(0, -.018f * Mathf.Min(1, elapsed / duration), 0);
        }
    }

    private IEnumerator rotateFlap(Vector3[] flapOutline, int flapIx, int btnIx, float angle, float duration, float? initialDelay)
    {
        yield return initialDelay != null ? new WaitForSeconds(initialDelay.Value) : null;

        var flap = FlapsParent.Find("Flap" + (4 * btnIx + flapIx));
        var elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            var origDelta = Time.deltaTime;
            var newDelta = origDelta;
            if (elapsed + origDelta > duration)
                newDelta = duration - elapsed;
            elapsed += origDelta;

            // These values are calculated inside the while loop because localToWorldMatrix changes if the bomb is rotated
            var pt = flap.localToWorldMatrix * new Vector4(-flapOutline[flapIx].x, flapOutline[flapIx].y, flapOutline[flapIx].z, 1);
            var pt2 = flap.localToWorldMatrix * new Vector4(-flapOutline[(flapIx + 1) % 4].x, flapOutline[(flapIx + 1) % 4].y, flapOutline[(flapIx + 1) % 4].z, 1);
            var axis = new Vector3(pt2.x - pt.x, pt2.y - pt.y, pt2.z - pt.z);

            flap.RotateAround(pt, axis, -angle * newDelta / duration);
        }
    }

    private void startBlinker(float delay)
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;
        _blinker = StartCoroutine(runBlinker(delay));
    }

    private void startBlinker()
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        _blinker = StartCoroutine(runBlinker());
    }

    private KMSelectable.OnInteractHandler HandlePress(int ix)
    {
        return delegate
        {
            if (_isSolved)
                return false;

            Buttons[ix].AddInteractionPunch();

            _makeSounds = true;
            Audio.PlaySoundAtTransform("Sound" + (ix + 7), Buttons[ix].transform);
            CancelInvoke("startBlinker");

            if (ix != _expectedInput[_stage][_subprogress])
            {
                Debug.LogFormat("[Simon Screams #{3}] Expected {0}, but you pressed {1}. Input reset. Now at stage {2} key 1.", _colors[_expectedInput[_stage][_subprogress]], _colors[ix], _stage + 1, _moduleId);
                Module.HandleStrike();
                _subprogress = 0;
                startBlinker(1.5f);
            }
            else
            {
                _subprogress++;
                var logStage = false;
                if (_subprogress == _expectedInput[_stage].Length)
                {
                    Leds[_stage].material = LitLed;
                    _stage++;
                    _subprogress = 0;
                    if (_stage == _expectedInput.Length)
                    {
                        Debug.LogFormat("[Simon Screams #{1}] Pressing {0} was correct. Module solved.", _colors[ix], _moduleId);
                        _isSolved = true;
                        StartCoroutine(victory(ix));
                        return false;
                    }

                    logStage = true;
                    startBlinker(1f);
                }
                else
                    startBlinker(5f);

                Debug.LogFormat("[Simon Screams #{3}] Pressing {0} was correct; now at stage {1} key {2}.", _colors[ix], _stage + 1, _subprogress + 1, _moduleId);
                if (logStage)
                    logCurrentStage();
            }

            StartCoroutine(flashUpOne(ix));
            return false;
        };
    }

    private IEnumerator victory(int ix)
    {
        alignFlaps(ix, -90, 1.5f, animation: true);
        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;
        Lights[ix].enabled = true;
        yield return new WaitForSeconds(.3f);
        Lights[ix].enabled = false;
        yield return new WaitForSeconds(.1f);
        Audio.PlaySoundAtTransform("Victory", Buttons[ix].transform);
        for (int i = 0; i < 13; i++)
        {
            Lights[(i + ix) % 6].enabled = true;
            Lights[(12 - i + ix) % 6].enabled = true;
            yield return new WaitForSeconds(.1f);
            Lights[(i + ix) % 6].enabled = false;
            Lights[(12 - i + ix) % 6].enabled = false;
        }
        Module.HandlePass();
        _finishedAnimating = true;
    }

    private IEnumerator runBlinker(float delay = 0)
    {
        yield return new WaitForSeconds(delay);

        if (_subprogress != 0)
        {
            Debug.LogFormat("[Simon Screams #{1}] Waited too long; input reset. Now at stage {0} key 1.", _stage + 1, _moduleId);
            _subprogress = 0;
        }
        while (!_isSolved)
        {
            for (int i = 0; i < _sequences[_stage].Length; i++)
            {
                if (_makeSounds)
                    Audio.PlaySoundAtTransform("Sound" + (_sequences[_stage][i] + 1), Buttons[(int) _colors[_sequences[_stage][i]]].transform);
                Lights[_sequences[_stage][i]].enabled = true;
                yield return new WaitForSeconds(.30f);
                Lights[_sequences[_stage][i]].enabled = false;
                yield return new WaitForSeconds(.10f);
            }
            yield return new WaitForSeconds(2.5f);
        }
    }

    private IEnumerator flashUpOne(int ix)
    {
        yield return null;
        Lights[ix].enabled = true;
        yield return new WaitForSeconds(.3f);
        Lights[ix].enabled = false;
    }

    private static int[][] generateSequences()
    {
        var seq = new int[maxFirstStageLength + numStages * maxStageExtra];
        seq[0] = Rnd.Range(0, 6);
        for (int i = 1; i < seq.Length; i++)
        {
            seq[i] = Rnd.Range(0, 5);
            if (seq[i] >= seq[i - 1])
                seq[i]++;
        }
        var arr = new int[numStages][];
        var len = Rnd.Range(minFirstStageLength, maxFirstStageLength + 1);
        for (int stage = 0; stage < numStages; stage++)
        {
            arr[stage] = seq.Subarray(0, len);
            len += Rnd.Range(minStageExtra, maxStageExtra + 1);
        }
        return arr;
    }

    void logCurrentStage()
    {
        var seq = _sequences[_stage];
        var applicableRow = _rowCriteria.IndexOf(cri => cri.Check(seq));

        Debug.LogFormat("[Simon Screams #{2}] Stage {0} sequence: {1}", _stage + 1, seq.Select(ix => _colors[ix]).JoinString(", "), _moduleId);
        Debug.LogFormat("[Simon Screams #{4}] Stage {0} column={1}, row={2} ({3})", _stage + 1, _colors[seq[_stageIxs[_stage]]], applicableRow + 1, _rowCriteria[applicableRow].Name, _moduleId);
        Debug.LogFormat("[Simon Screams #{2}] Stage {0} expected keypresses: {1}", _stage + 1, _expectedInput[_stage].Select(ix => _colors[ix]).JoinString(", "), _moduleId);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Press the correct colors for each round with “!{0} press Blue Orange Yellow” or “!{0} B O Y”. Permissible colors are: Red, Orange, Yellow, Green, Blue, Purple. Use “!{0} disco” or “!{0} lasershow” to have a good time. Use “!{0} colorblind” to show the colors of the buttons.";
    private readonly bool TwitchShouldCancelCommand = false;
#pragma warning restore 414

    private IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        while (!_isSolved)
        {
            Buttons[_expectedInput[_stage][_subprogress]].OnInteract();
            yield return new WaitForSeconds(0.4f);
        }
        while (!_finishedAnimating)
        {
            yield return true;
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length == 1 && pieces[0] == "colorblind")
            return enableColorblindMode();

        if (pieces.Length == 1 && pieces[0] == "disco")
            return disco();

        if ((pieces.Length == 1 && pieces[0] == "lasershow") || (pieces.Length == 2 && pieces[0] == "laser" && pieces[1] == "show"))
            return laserShow();

        return processPress(pieces);
    }

    private IEnumerator enableColorblindMode()
    {
        yield return null;
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(.15f);
            ColorblindIndicators[i].gameObject.SetActive(true);
        }
    }

    private IEnumerator disco()
    {
        yield return "antitroll Aw man! Can't play awesome disco track.";

        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;
        for (int i = 0; i < 31; i++)
        {
            var ix = Rnd.Range(0, 6);
            Audio.PlaySoundAtTransform("Sound" + (ix + 1), Buttons[(int) _colors[ix]].transform);
            Lights[ix].enabled = true;
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                    Leds[k].material = Rnd.Range(0, 2) == 0 ? UnlitLed : LitLed;
                yield return new WaitForSeconds(.1f);
            }
            Lights[ix].enabled = false;
            if (TwitchShouldCancelCommand)
                break;
        }
        for (int j = 0; j < 3; j++)
            Leds[j].material = j < _stage ? LitLed : UnlitLed;
        startBlinker(1.5f);
        if (TwitchShouldCancelCommand)
        {
            yield return "sendtochat Aw man! Disco cut short!";
            yield return "cancelled";
        }
        yield break;
    }

    private IEnumerator laserShow()
    {
        yield return "antitroll Aw man! I can't put on a laser show.";

        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;

        for (int j = 0; j < 4; j++)
        {
            var ix = Rnd.Range(0, 6);
            for (int i = 0; i < 12; i++)
            {
                if (i % 3 == 0)
                    Audio.PlaySoundAtTransform("Sound" + ((ix + i / 3) % 6 + 1), Buttons[(int) _colors[ix]].transform);
                Lights[((j % 2 == 0 ? i : 12 - i) + ix) % 6].enabled = true;
                Leds[i % 3].material = LitLed;
                yield return new WaitForSeconds(.1f);
                Leds[i % 3].material = UnlitLed;
                Lights[((j % 2 == 0 ? i : 12 - i) + ix) % 6].enabled = false;
                if (TwitchShouldCancelCommand)
                    goto cutShort;
            }
            Audio.PlaySoundAtTransform("Victory", Buttons[ix].transform);
            for (int i = 0; i < (j == 3 ? 13 : 12); i++)
            {
                Lights[(i + ix) % 6].enabled = true;
                Lights[(12 - i + ix) % 6].enabled = true;
                Leds[i % 3].material = LitLed;
                yield return new WaitForSeconds(.1f);
                Lights[(i + ix) % 6].enabled = false;
                Lights[(12 - i + ix) % 6].enabled = false;
                Leds[i % 3].material = UnlitLed;
                if (TwitchShouldCancelCommand)
                    goto cutShort;
            }
        }

        cutShort:;
        for (int j = 0; j < 3; j++)
            Leds[j].material = j < _stage ? LitLed : UnlitLed;
        startBlinker(.5f);
        if (TwitchShouldCancelCommand)
        {
            yield return "sendtochat Aw man! Laser show cut short!";
            yield return "cancelled";
        }
        yield break;
    }

    private IEnumerator processPress(string[] pieces)
    {
        var skip = 0;
        if (pieces.Length > 0 && pieces[0] == "press")
            skip = 1;

        var buttons = new List<KMSelectable>();
        var colors = new[] { SimonColor.Red, SimonColor.Orange, SimonColor.Yellow, SimonColor.Green, SimonColor.Blue, SimonColor.Purple };
        var colorsStr = colors.Select(c => c.ToString().ToLowerInvariant()).ToArray();
        foreach (var piece in pieces.Skip(skip))
        {
            var ix = colorsStr.IndexOf(cs => cs.Equals(piece, StringComparison.InvariantCultureIgnoreCase) || (piece.Length == 1 && cs.StartsWith(piece)));
            if (ix == -1)
                yield break;
            buttons.Add(Buttons[Array.IndexOf(_colors, colors[ix])]);
        }

        yield return null;

        foreach (var btn in buttons)
        {
            btn.OnInteract();
            if (_isSolved)
            {
                yield return "solve";
                yield break;
            }
            yield return new WaitForSeconds(.4f);
        }
    }
}

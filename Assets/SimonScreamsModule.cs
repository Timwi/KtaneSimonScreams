using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public KMSelectable MainSelectable;
    public KMSelectable[] Buttons;
    public Light[] Lights;
    public Material[] Materials;
    public Transform FlapsParent;
    public Transform KeypadParent;

    public MeshRenderer[] Leds;
    public Material UnlitLed;
    public Material LitLed;

    private SimonColor[] _colors;
    private int[][] _sequences;
    private int[][] _expectedInput;
    private int _stage;
    private int _subprogress;
    private bool _isActivated;
    private bool _isSolved;
    private int _red, _yellow, _blue;
    private bool _makeSounds;
    private Coroutine _blinker;

    private static Criterion[] _rowCriteria = new Criterion[] { new Row1Criterion(), new Row2Criterion(), new Row3Criterion(), new Row4Criterion(), new Row5Criterion(), new Row6Criterion() };
    private static string[][] _largeTable = Ut.NewArray(
        new[] { "FFC", "CEH", "HAF", "ECD", "DDE", "AHA" },
        new[] { "AHF", "DFC", "ECH", "CDE", "FEA", "HAD" },
        new[] { "DED", "ECF", "FHE", "HAA", "AFH", "CDC" },
        new[] { "HCE", "ADA", "CFD", "DHH", "EAC", "FEF" },
        new[] { "CAH", "FHD", "DDA", "AEC", "HCF", "EFE" },
        new[] { "EDA", "HAE", "AEC", "FFF", "CHD", "DCH" }
    );
    private static string _smallTableColumns = "ACDEFH";
    private static SimonColor[][] _smallTable = Ut.NewArray(
        new[] { SimonColor.Yellow, SimonColor.Orange, SimonColor.Green, SimonColor.Red, SimonColor.Blue, SimonColor.Purple },
        new[] { SimonColor.Purple, SimonColor.Yellow, SimonColor.Red, SimonColor.Blue, SimonColor.Orange, SimonColor.Green },
        new[] { SimonColor.Orange, SimonColor.Green, SimonColor.Blue, SimonColor.Purple, SimonColor.Red, SimonColor.Yellow },
        new[] { SimonColor.Green, SimonColor.Blue, SimonColor.Orange, SimonColor.Yellow, SimonColor.Purple, SimonColor.Red },
        new[] { SimonColor.Red, SimonColor.Purple, SimonColor.Yellow, SimonColor.Orange, SimonColor.Green, SimonColor.Blue },
        new[] { SimonColor.Blue, SimonColor.Red, SimonColor.Purple, SimonColor.Green, SimonColor.Yellow, SimonColor.Orange }
    );
    private static Func<KMBombInfo, bool>[] _smallTableRowCriteria = Ut.NewArray<Func<KMBombInfo, bool>>(
        m => m.GetIndicators().Count() >= 3,
        m => m.GetPortCount() >= 3,
        m => m.GetSerialNumberNumbers().Count() >= 3,
        m => m.GetSerialNumberLetters().Count() >= 3,
        m => m.GetBatteryCount() >= 3,
        m => m.GetBatteryHolderCount() >= 3
    );
    private static string[] _smallTableRowCriteriaNames = Ut.NewArray(
        "≥ 3 indicators",
        "≥ 3 ports",
        "≥ 3 numbers in serial number",
        "≥ 3 letters in serial number",
        "≥ 3 batteries",
        "≥ 3 battery holders"
    );

    private const int numStages = 3, minFirstStageLength = 3, maxFirstStageLength = 5, minStageExtra = 1, maxStageExtra = 2;

    private static Vector3[] _unrotatedFlapOutline;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

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
        _isActivated = false;
        _isSolved = false;
        _stage = 0;
        _subprogress = 0;
        _colors = ((SimonColor[]) Enum.GetValues(typeof(SimonColor))).Shuffle();
        _red = _colors.IndexOf(SimonColor.Red);
        _yellow = _colors.IndexOf(SimonColor.Yellow);
        _blue = _colors.IndexOf(SimonColor.Blue);
        _sequences = generateSequences();
        _makeSounds = false;

        for (int i = 0; i < 6; i++)
        {
            var mat = Materials[(int) _colors[i]];
            Buttons[i].GetComponent<MeshRenderer>().material = mat;
            Lights[i].color = mat.color;

            var j = i;
            Buttons[i].OnInteract = delegate { HandlePress(j); return false; };
        }

        for (int i = 0; i < 3; i++)
            Leds[i].material = UnlitLed;

        Debug.LogFormat("[Simon Screams #{1}] Colors in clockwise order are: {0}", _colors.JoinString(", "), _moduleId);

        startBlinker(1.5f);
        alignFlaps(0, 90, .01f);
        Module.OnActivate = ActivateModule;
        Bomb.OnBombExploded = delegate { StopAllCoroutines(); };

        float scalar = transform.lossyScale.x;
        foreach (Light light in Lights)
            light.range *= scalar;
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

    private void HandlePress(int ix)
    {
        if (!_isActivated || _isSolved)
            return;

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
                    return;
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

    void ActivateModule()
    {
        _isActivated = true;

        var smallTableRows = _smallTableRowCriteria.Select(cri => cri(Bomb)).ToArray();
        Debug.LogFormat("[Simon Screams #{1}] Small table applicable rows are: {0}", smallTableRows.SelectIndexWhere(b => b).Select(ix => _smallTableRowCriteriaNames[ix]).JoinString(", "), _moduleId);
        var ryb = new[] { _red, _yellow, _blue };
        _expectedInput = _sequences.Select((seq, stage) =>
        {
            var applicableColumn = _colors[seq[stage]];
            var applicableRow = _rowCriteria.IndexOf(cri => cri.Check(seq, ryb));
            var smallTableColumn = _smallTableColumns.IndexOf(_largeTable[applicableRow][(int) applicableColumn][stage]);
            return _smallTable.Where((row, ix) => smallTableRows[ix]).Select(row => _colors.IndexOf(row[smallTableColumn])).ToArray();
        }).ToArray();

        logCurrentStage();
    }

    void logCurrentStage()
    {
        var seq = _sequences[_stage];
        var applicableRow = _rowCriteria.IndexOf(cri => cri.Check(seq, new[] { _red, _yellow, _blue }));

        Debug.LogFormat("[Simon Screams #{2}] Stage {0} sequence: {1}", _stage + 1, seq.Select(ix => _colors[ix]).JoinString(", "), _moduleId);
        Debug.LogFormat("[Simon Screams #{4}] Stage {0} column={1}, row={2} ({3})", _stage + 1, _colors[seq[_stage]], applicableRow + 1, _rowCriteria[applicableRow].Name, _moduleId);
        Debug.LogFormat("[Simon Screams #{2}] Stage {0} expected keypresses: {1}", _stage + 1, _expectedInput[_stage].Select(ix => _colors[ix]).JoinString(", "), _moduleId);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Press the correct colors for each round with “!{0} press Blue Orange Yellow” or “!{0} B O Y”. Permissible colors are: Red, Orange, Yellow, Green, Blue, Purple. Use “!{0} disco” or “!{0} lasershow” to have a good time.";
    private readonly bool TwitchShouldCancelCommand = false;
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (pieces.Length == 1 && pieces[0] == "disco")
            return disco();

        if ((pieces.Length == 1 && pieces[0] == "lasershow") || (pieces.Length == 2 && pieces[0] == "laser" && pieces[1] == "show"))
            return laserShow();

        return processPress(pieces);
    }

    private IEnumerator disco()
    {
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

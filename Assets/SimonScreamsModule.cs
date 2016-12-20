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

    public KMSelectable[] Buttons;
    public Light[] Lights;
    public Material[] Materials;

    private SimonColor[] _colors;
    private int[][] _sequences;
    private int[][] _expectedInput;
    private int _stage;
    private int _subprogress;
    private bool _isActivated;
    private bool _isSolved;
    private int _red, _yellow, _blue, _orange;
    private bool _makeSounds;
    private Coroutine _blinker;

    private static Criterion[] _columnCriteria = new Criterion[] { new Col1Criterion(), new Col2Criterion(), new Col3Criterion(), new Col4Criterion(), new Col5Criterion(), new Col6Criterion() };
    private static Criterion[] _rowCriteria = new Criterion[] { new Row1Criterion(), new Row2Criterion(), new Row3Criterion(), new Row4Criterion(), new Row5Criterion(), new Row6Criterion() };
    private static string[][] _largeTable = Ut.NewArray(
        new[] { "HCE", "ADA", "CFD", "DHH", "EAC", "FEF" },
        new[] { "DED", "ECF", "FHE", "HAA", "AFH", "CDC" },
        new[] { "FFC", "CEH", "HAF", "ECD", "DDE", "AHA" },
        new[] { "AHF", "DFC", "ECH", "CDE", "FEA", "HAD" },
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
        m => m.GetIndicators().Count() >= 2,
        m => m.GetPorts().AnyDuplicates(),
        m => m.GetPortPlates().Any(pp => pp.Length == 0),
        m => m.GetSerialNumberLetters().Count() == 4,
        m => m.GetBatteryHolderCount() >= 3,
        m => true
    );
    private static string[] _smallTableRowCriteriaNames = Ut.NewArray(
        "≥ 2 indicators",
        "duplicate port",
        "empty port plate",
        "4 letters in #",
        "≥ 3 b.h.",
        "always"
    );

    void Start()
    {
        _isActivated = false;
        _isSolved = false;
        _stage = 0;
        _subprogress = 0;
        _colors = ((SimonColor[]) Enum.GetValues(typeof(SimonColor))).Shuffle();
        _red = _colors.IndexOf(SimonColor.Red);
        _yellow = _colors.IndexOf(SimonColor.Yellow);
        _blue = _colors.IndexOf(SimonColor.Blue);
        _orange = _colors.IndexOf(SimonColor.Orange);
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

        Debug.LogFormat("[Simon Screams] Started. Sequences are:\n{0}", _sequences.Select((seq, i) => string.Format("Stage {0}: {1}", i, seq.Select(ix => _colors[ix]).JoinString(", "))).JoinString("\n"));

        Module.OnActivate = ActivateModule;

        startBlinker(1.5f);
    }

    private void startBlinker(float delay)
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;
        Invoke("startBlinker", delay);
    }

    private void startBlinker()
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        _blinker = StartCoroutine(runBlinker());
    }

    private void HandlePress(int ix)
    {
        Buttons[ix].AddInteractionPunch();

        if (!_isActivated || _isSolved)
            return;

        _makeSounds = true;
        Audio.PlaySoundAtTransform("Sound" + (ix + 7), Buttons[ix].transform);
        CancelInvoke("startBlinker");

        if (ix != _expectedInput[_stage][_subprogress])
        {
            Debug.LogFormat("[Simon Screams] Expected {0}, but you pressed {1}. Input reset. Now at stage {2} key 1.", _colors[_expectedInput[_stage][_subprogress]], _colors[ix], _stage + 1);
            Module.HandleStrike();
            _subprogress = 0;
            startBlinker(1.5f);
        }
        else
        {
            _subprogress++;
            if (_subprogress == _expectedInput[_stage].Length)
            {
                _stage++;
                _subprogress = 0;
                if (_stage == _expectedInput.Length)
                {
                    Debug.LogFormat("[Simon Screams] Pressing {0} was correct. Module solved.", _colors[ix]);
                    _isSolved = true;
                    if (_blinker != null)
                        StopCoroutine(_blinker);
                    Invoke("pass", .5f);
                    return;
                }

                startBlinker(1f);
            }
            else
                startBlinker(5f);
            Debug.LogFormat("[Simon Screams] Pressing {0} was correct; now at stage {1} key {2}.", _colors[ix], _stage + 1, _subprogress + 1);
        }
    }

    private void pass()
    {
        Module.HandlePass();
    }

    private IEnumerator runBlinker()
    {
        if (_subprogress != 0)
        {
            Debug.LogFormat("[Simon Screams] Waited too long; input reset. Now at stage {0} key 1.", _stage + 1);
            _subprogress = 0;
        }
        while (!_isSolved)
        {
            for (int i = 0; i < _sequences[_stage].Length; i++)
            {
                if (_makeSounds)
                    Audio.PlaySoundAtTransform("Sound" + (_sequences[_stage][i] + 1), Buttons[(int) _colors[_sequences[_stage][i]]].transform);
                Lights[_sequences[_stage][i]].enabled = true;
                yield return new WaitForSeconds(.25f);
                Lights[_sequences[_stage][i]].enabled = false;
                yield return new WaitForSeconds(.05f);
            }
            yield return new WaitForSeconds(2.5f);
        }
    }

    private static int[][] generateSequences()
    {
        var seq = generateSequence().ToArray();
        var arr = new int[3][];
        var len = 6;
        for (int stage = 0; stage < 3; stage++)
        {
            arr[stage] = seq.Subarray(0, len);
            len += Rnd.Range(0, 3) + 1;
        }
        return arr;
    }

    private static IEnumerable<int> generateSequence()
    {
        var last = Rnd.Range(0, 6);
        yield return last;
        var num = 12;
        for (int i = 1; i < num; i++)
        {
            var next = Rnd.Range(0, 5);
            if (next >= last)
                next++;
            yield return next;
            last = next;
        }
    }

    void ActivateModule()
    {
        _isActivated = true;

        var smallTableRows = _smallTableRowCriteria.Select(cri => cri(Bomb)).ToArray();
        Debug.LogFormat("[Simon Screams] Small table applicable rows are: {0}", smallTableRows.SelectIndexWhere(b => b).Select(ix => _smallTableRowCriteriaNames[ix]).JoinString(", "));
        var ryb = new[] { _red, _yellow, _blue };
        _expectedInput = _sequences.Select((seq, stage) =>
        {
            var applicableColumn = _columnCriteria.IndexOf(cri => cri.Check(seq, ryb, _orange));
            var applicableRow = _rowCriteria.IndexOf(cri => cri.Check(seq, ryb, _orange));
            Debug.LogFormat("[Simon Screams] Stage {0} column=“{1}”, row=“{2}”", stage + 1, _columnCriteria[applicableColumn].Name, _rowCriteria[applicableRow].Name);
            var smallTableColumn = _smallTableColumns.IndexOf(_largeTable[applicableRow][applicableColumn][stage]);
            return _smallTable.Where((row, ix) => smallTableRows[ix]).Select(row => _colors.IndexOf(row[smallTableColumn])).ToArray();
        }).ToArray();

        Debug.LogFormat("[Simon Screams] Activated. Expected keypresses are:\n{0}", _expectedInput.Select((seq, i) => string.Format("Stage {0}: {1}", i, seq.Select(ix => _colors[ix]).JoinString(", "))).JoinString("\n"));
    }
}

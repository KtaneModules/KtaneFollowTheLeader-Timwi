using System;
using System.Collections.Generic;
using System.Linq;
using FollowTheLeaderNS;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Follow The Leader
/// Created by TheAuthorOfOZ
/// Implemented by Timwi
/// </summary>
public class FollowTheLeaderModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMSelectable Selectable;
    public KMAudio Audio;

    public Material[] ColorMaterials;
    public Material CopperMaterial;
    public Mesh[] WireColliderMeshes;

    static string[] ColorNames = { "Red", "Green", "White", "Yellow", "Blue", "Black" };

    class WireInfo
    {
        // 0–11
        public int ConnectedFrom;
        public int ConnectedTo;
        public bool DoesSkip { get { return ConnectedTo != (ConnectedFrom + 1) % 12; } }

        // 0 = red, 1 = green, 2 = white, 3 = yellow, 4 = blue, 5 = black
        public int Color;
        public bool MustCut;
        public bool IsCut;

        public void Activate(Transform transform, Material[] colorMats, Material copperMat, List<WireInfo> expectedCuts, KMBombModule bomb)
        {
            // The irony of using one RNG to seed another RNG isn’t lost on me
            var seed = Rnd.Range(0, int.MaxValue);
            var rnd = new System.Random(seed);
            var lengthIndex = DoesSkip ? ConnectedFrom % 2 : 2;

            transform.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, Color, MeshGenerator.WirePiece.Uncut, false);
            transform.GetComponent<MeshRenderer>().material = colorMats[Color];

            rnd = new System.Random(seed);
            var wireHighlight = transform.Find(string.Format("Wire {0}-to-{1} highlight", ConnectedFrom + 1, ConnectedTo + 1));
            var highlightMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, Color, MeshGenerator.WirePiece.Uncut, true);
            wireHighlight.GetComponent<MeshFilter>().mesh = highlightMesh;

            var clone = wireHighlight.Find("Highlight(Clone)");
            if (clone != null)
                clone.GetComponent<MeshFilter>().mesh = highlightMesh;

            rnd = new System.Random(seed);
            var cutMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, Color, MeshGenerator.WirePiece.Cut, false);
            rnd = new System.Random(seed);
            var cutMeshHighlight = MeshGenerator.GenerateWire(rnd, lengthIndex, Color, MeshGenerator.WirePiece.Cut, true);
            rnd = new System.Random(seed);
            var copperMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, Color, MeshGenerator.WirePiece.Copper, false);

            IsCut = false;
            transform.GetComponent<KMSelectable>().OnInteract += delegate
            {
                if (IsCut)
                {
                    bomb.HandleStrike();
                    return false;
                }
                IsCut = true;
                transform.GetComponent<MeshFilter>().mesh = cutMesh;
                wireHighlight.GetComponent<MeshFilter>().mesh = cutMeshHighlight;
                var hClone = wireHighlight.Find("Highlight(Clone)");
                if (hClone != null)
                    hClone.GetComponent<MeshFilter>().mesh = cutMeshHighlight;

                var child = new GameObject();
                child.transform.parent = transform;
                child.transform.localRotation = new Quaternion(0, 0, 0, 0);
                child.transform.localScale = new Vector3(1, 1, 1);
                child.transform.localPosition = new Vector3(0, 0, 0);
                child.AddComponent<MeshFilter>().mesh = copperMesh;
                child.AddComponent<MeshRenderer>().material = copperMat;

                if (expectedCuts.Count == 0 || expectedCuts[0] != this)
                    bomb.HandleStrike();
                else
                {
                    Debug.Log("Correct");
                    while (expectedCuts.Count > 0 && expectedCuts[0].IsCut)
                        expectedCuts.RemoveAt(0);
                    if (expectedCuts.Count == 0)
                        bomb.HandlePass();
                }
                return false;
            };
            transform.gameObject.SetActive(true);
        }

        public override string ToString()
        {
            return string.Format("Wire {0}-to-{1} ({2})", ConnectedFrom + 1, ConnectedTo + 1, ColorNames[Color]);
        }
    }

    int _numBatteries;
    string _serial;
    bool _hasRJ;
    bool _hasLitCLR;

    List<WireInfo> _wireInfos;
    List<WireInfo> _expectedCuts;

    void Start()
    {
        Module.OnActivate += ActivateModule;

        _expectedCuts = new List<WireInfo>();
        _wireInfos = new List<WireInfo>();
        var currentPeg = 0;
        do
        {
            // Randomly skip a peg about 25% of the time, but we have to
            // go back to peg #0 if we’re on peg #11. Also, we want at least
            // 8 wires, so we need to stop skipping if we don’t have enough wires.
            var skip = currentPeg >= 11 || currentPeg >= _wireInfos.Count + 4 ? false : Rnd.Range(0, 4) == 0;
            var nextPeg = (currentPeg + (skip ? 2 : 1)) % 12;
            _wireInfos.Add(new WireInfo
            {
                ConnectedFrom = currentPeg,
                ConnectedTo = nextPeg,
                Color = Rnd.Range(0, 6)
            });
            currentPeg = nextPeg;
        }
        while (currentPeg != 0);

        // Since the above will never skip peg #0, rotate the arrangement by a random amount
        // so that all pegs have equal chances of being skipped.
        var rotation = Rnd.Range(0, 12);
        for (int i = 0; i < _wireInfos.Count; i++)
        {
            _wireInfos[i].ConnectedFrom = (_wireInfos[i].ConnectedFrom + rotation) % 12;
            _wireInfos[i].ConnectedTo = (_wireInfos[i].ConnectedTo + rotation) % 12;
        }

        /* — CODE TO GENERATE ALL WIRE OBJECTS
        for (int from = 0; from < 12; from++)
        {
            foreach (var skip in new[] { false, true })
            {
                var to = (from + (skip ? 2 : 1)) % 12;
                var mesh = WireColliderMeshes[skip ? from % 2 : 2];

                // The irony of using one RNG to seed another RNG isn’t lost on me
                var seed = Rnd.Range(0, int.MaxValue);
                var rnd = new System.Random(seed);

                var wire = new GameObject();
                var meshFilter = wire.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                var meshRenderer = wire.AddComponent<MeshRenderer>();
                meshRenderer.material = ColorMaterials[0];
                var selectable = wire.AddComponent<KMSelectable>();
                selectable.Parent = Selectable;

                rnd = new System.Random(seed);
                var wireHighlight = new GameObject();
                wireHighlight.transform.parent = wire.transform;
                meshFilter = wireHighlight.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                var highlightable = wireHighlight.AddComponent<KMHighlightable>();
                highlightable.HighlightScale = new Vector3(1, 1, 1);
                selectable.Highlight = highlightable;

                wire.name = string.Format("Wire {0}-to-{1}", from + 1, to + 1);
                wireHighlight.name = string.Format("Wire {0}-to-{1} highlight", from + 1, to + 1);

                double x, z;
                var angle = MeshGenerator.GetWirePosAndAngle(from, to, out x, out z);

                wire.transform.parent = Module.transform;
                wire.transform.Translate(new Vector3((float) x, 0.023f, (float) z));
                wire.transform.Rotate(new Vector3(0, (float) (-angle), 0));
                wire.transform.Rotate(new Vector3(-90, 0, 0));
            }
        }
        /**/

        // Disable all the wires (we will later re-enable the ones we need)
        for (int from = 0; from < 12; from++)
            foreach (var skip in new[] { false, true })
                Module.transform.Find(string.Format("Wire {0}-to-{1}", from + 1, (from + (skip ? 2 : 1)) % 12 + 1)).gameObject.SetActive(false);

        for (int i = 0; i < _wireInfos.Count; i++)
            _wireInfos[i].Activate(Module.transform.Find(string.Format("Wire {0}-to-{1}", _wireInfos[i].ConnectedFrom + 1, _wireInfos[i].ConnectedTo + 1)), ColorMaterials, CopperMaterial, _expectedCuts, Module);

        // Code from sircharles
        //Selectable.Children = _wireInfos.Select(wi => wi.Selectable).ToArray();
        //var modSelectable = GetComponent("ModSelectable");
        //if (modSelectable != null)
        //    modSelectable.GetType().GetMethod("CopySettingsFromProxy").Invoke(modSelectable, null);
    }

    static Func<WireInfo, WireInfo, WireInfo, WireInfo, bool>[] rules = Ex.NewArray<Func<WireInfo, WireInfo, WireInfo, WireInfo, bool>>(
        // A or N: The previous wire is not yellow or blue or green.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("A: cut if {0} not 1,3,4", p1.Color)); return !new[] { 1, 3, 4 }.Contains(p1.Color); },
        // B or O: The previous wire leads to an even numbered plug.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("B: cut if {0} % 2 == 1", p1.ConnectedTo)); return p1.ConnectedTo % 2 == 1; },
        // C or P: The previous wire is cut.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("C: cut if {0} true", p1.MustCut)); return p1.MustCut; },
        // D or Q: The previous wire is red or blue or black.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("D: cut if {0} is 0,4,5", p1.Color)); return new[] { 0, 4, 5 }.Contains(p1.Color); },
        // E or R: The three previous wires are not all the same color.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("E: cut if {0},{1},{2} are not same", p1.Color, p2.Color, p3.Color)); return p3.Color != p1.Color || p2.Color != p1.Color; },
        // F or S: Exactly one of the previous two wires are cut.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("F: cut if {0} xor {1}", p1.MustCut, p2.MustCut)); return p1.MustCut ^ p2.MustCut; },
        // G or T: The previous wire is yellow or white or green.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("G: cut if {0} is 1,2,3", p1.Color)); return new[] { 1, 2, 3 }.Contains(p1.Color); },
        // H or U: The previous wire is not cut.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("H: cut if {0} is false", p1.MustCut)); return !p1.MustCut; },
        // I or V: The previous wire skipped a position.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("I: cut if {0} is true", p1.DoesSkip)); return p1.DoesSkip; },
        // J or W: The previous wire is not white or black or red.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("J: cut if {0} not 0,2,5", p1.Color)); return !new[] { 0, 2, 5 }.Contains(p1.Color); },
        // K or X: The previous wire does not lead to a position labeled 6 or less.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("K: cut if {0} ≥ 6", p1.ConnectedTo)); return p1.ConnectedTo >= 6; },
        // L or Y: The previous wire is the same color.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("L: cut if {0} = {1}", p0.Color, p1.Color)); return p1.Color == p0.Color; },
        // M or Z: One or neither of the previous two wires are cut.
        (p3, p2, p1, p0) => { Debug.Log(string.Format("M: cut if !({0} && {1})", p1.MustCut, p2.MustCut)); return !(p1.MustCut && p2.MustCut); }
    );

    void ActivateModule()
    {
        _serial = Bomb.GetSerialNumber();
        if (_serial == null)
        {
            // Generate random values for testing in Unity
            _serial = string.Join("", Enumerable.Range(0, 6).Select(i => Rnd.Range(0, 36)).Select(i => i < 10 ? ((char) ('0' + i)).ToString() : ((char) ('A' + i - 10)).ToString()).ToArray());
            _hasRJ = Rnd.Range(0, 2) == 0;
            _numBatteries = Rnd.Range(0, 7);
            _hasLitCLR = Rnd.Range(0, 2) == 0;
        }
        else
        {
            _numBatteries = Bomb.GetBatteryCount();
            _hasRJ = Bomb.GetPortCount(KMBombInfoExtensions.KnownPortType.RJ45) > 0;
            _hasLitCLR = Bomb.GetOnIndicators().Contains("CLR");
        }

        Debug.Log("[FollowTheLeader] Serial number is " + _serial);
        Debug.Log("[FollowTheLeader] Number of batteries: " + _numBatteries);
        Debug.Log("[FollowTheLeader] Has RJ-45 port: " + (_hasRJ ? "Yes" : "No"));
        Debug.Log("[FollowTheLeader] Has lit CLR indicator: " + (_hasLitCLR ? "Yes" : "No"));

        // Figure out the starting wire (as index into wireInfos, rather than peg number)
        int curIndex;
        var serialFirstNumeral = _serial.Where(ch => ch >= '0' && ch <= '9').FirstOrNull();
        if (_hasRJ && (curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 3 && wi.ConnectedTo == 4)) != -1)
        {
        }
        else if ((curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == _numBatteries)) != -1)
        {
        }
        else if (serialFirstNumeral != null && (curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == serialFirstNumeral.Value - '0')) != -1)
        {
        }
        else if (_hasLitCLR)
        {
            _expectedCuts.Clear();
            _expectedCuts.AddRange(_wireInfos.OrderByDescending(w => w.ConnectedFrom));
            return;
        }
        else if ((curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 0)) != -1)
        {
        }
        else
            curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 1);

        Debug.Log("[Follow the Leader] Starting at wire: " + _wireInfos[curIndex]);

        // The starting step corresponds to the first letter in the serial number.
        var curStep = _serial.Where(ch => ch >= 'A' && ch <= 'Z').Select(ch => ch - 'A').FirstOrDefault() % rules.Length;
        // If the wire at the starting plug is red, green, or white, progress through the steps in reverse alphabetical order instead.
        var reverse = new[] { 0, 1, 2 }.Contains(_wireInfos[curIndex].Color);

        _expectedCuts.Clear();

        // Finally, determine which wires need cutting
        for (int i = 0; i < _wireInfos.Count; i++)
        {
            if (i == 0)
            {
                // Always cut the first one.
                _wireInfos[curIndex].MustCut = true;
            }
            else
            {
                _wireInfos[curIndex].MustCut = rules[curStep](
                    _wireInfos[(curIndex + _wireInfos.Count - 3) % _wireInfos.Count],
                    _wireInfos[(curIndex + _wireInfos.Count - 2) % _wireInfos.Count],
                    _wireInfos[(curIndex + _wireInfos.Count - 1) % _wireInfos.Count],
                    _wireInfos[curIndex]
                );
                curStep = (curStep + rules.Length + (reverse ? -1 : 1)) % rules.Length;
            }

            if (_wireInfos[curIndex].MustCut)
                _expectedCuts.Add(_wireInfos[curIndex]);

            curIndex = (curIndex + 1) % _wireInfos.Count;
        }

        Debug.Log("[FollowTheLeader] My expectation (" + _expectedCuts.Count + "): cut " + string.Join(", ", _expectedCuts.Select(wi => string.Format("{0}-to-{1} ({2})", wi.ConnectedFrom + 1, wi.ConnectedTo + 1, ColorNames[wi.Color])).ToArray()));
    }
}

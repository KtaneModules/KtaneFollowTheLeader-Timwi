﻿using System;
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

        private KMSelectable _selectable;
        public KMSelectable Selectable { get { return _selectable; } }

        public void Activate(Transform transform, Material[] colorMats, List<WireInfo> expectedCuts, KMBombModule bomb)
        {
            // The irony of using one RNG to seed another RNG isn’t lost on me
            var seed = Rnd.Range(0, int.MaxValue);
            var rnd = new System.Random(seed);
            var lengthIndex = DoesSkip ? ConnectedFrom % 2 : 2;

            transform.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, MeshGenerator._wireRadius);
            transform.GetComponent<MeshRenderer>().material = colorMats[Color];

            rnd = new System.Random(seed);
            var wireHighlight = transform.Find(string.Format("Wire {0}-to-{1} highlight", ConnectedFrom + 1, ConnectedTo + 1));
            wireHighlight.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, MeshGenerator._wireRadiusHighlight);

            _selectable = transform.GetComponent<KMSelectable>();
            _selectable.OnInteract += delegate
            {
                if (expectedCuts.Count == 0 || expectedCuts[0] != this)
                    bomb.HandleStrike();
                else
                {
                    Debug.Log("Correct");
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
            return string.Format("<From={0}, To={1}, Color={2}>", ConnectedFrom + 1, ConnectedTo + 1, ColorNames[Color]);
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
        */

        // Disable all the wires (we will later re-enable the ones we need)
        for (int from = 0; from < 12; from++)
            foreach (var skip in new[] { false, true })
                Module.transform.Find(string.Format("Wire {0}-to-{1}", from + 1, (from + (skip ? 2 : 1)) % 12 + 1)).gameObject.SetActive(false);

        for (int i = 0; i < _wireInfos.Count; i++)
            _wireInfos[i].Activate(Module.transform.Find(string.Format("Wire {0}-to-{1}", _wireInfos[i].ConnectedFrom + 1, _wireInfos[i].ConnectedTo + 1)), ColorMaterials, _expectedCuts, Module);

        // Code from sircharles
        Selectable.Children = _wireInfos.Select(wi => wi.Selectable).ToArray();
        var modSelectable = GetComponent("ModSelectable");
        if (modSelectable != null)
            modSelectable.GetType().GetMethod("CopySettingsFromProxy").Invoke(modSelectable, null);
    }

    static T[] newArray<T>(params T[] array) { return array; }

    static Func<WireInfo, WireInfo, WireInfo, WireInfo, bool>[] rules = newArray(
        // A or N: The previous wire is not yellow or blue or green.
        (p3, p2, p1, p0) => !new[] { 1, 3, 4 }.Contains(p1.Color),
        // B or O: The previous wire leads to an even numbered plug.
        (p3, p2, p1, p0) => p1.ConnectedTo % 2 == 1,
        // C or P: The previous wire is cut.
        (p3, p2, p1, p0) => p1.MustCut,
        // D or Q: The previous wire is red or blue or black.
        (p3, p2, p1, p0) => new[] { 0, 4, 5 }.Contains(p1.Color),
        // E or R: The three previous wires are not all the same color.
#error this is wrong
        (p3, p2, p1, p0) => p3 == null || p2 == null || p3.Color != p1.Color || p2.Color != p1.Color,
        // F or S: Exactly one of the previous two wires are cut.
        (p3, p2, p1, p0) => p1.MustCut ^ (p2 != null && p2.MustCut),
        // G or T: The previous wire is yellow or white or green.
        (p3, p2, p1, p0) => new[] { 1, 2, 3 }.Contains(p1.Color),
        // H or U: The previous wire is not cut.
        (p3, p2, p1, p0) => !p1.MustCut,
        // I or V: The previous wire skipped a position.
        (p3, p2, p1, p0) => p1.DoesSkip,
        // J or W: The previous wire is not white or black or red.
        (p3, p2, p1, p0) => !new[] { 0, 2, 5 }.Contains(p1.Color),
        // K or X: The previous wire does not lead to a position labeled 6 or less.
        (p3, p2, p1, p0) => p1.ConnectedTo >= 6,
        // L or Y: The previous wire is the same color.
        (p3, p2, p1, p0) => p1.Color == p0.Color,
        // M or Z: One or neither of the previous two wires are cut.
        (p3, p2, p1, p0) => !(p1.MustCut && p2 != null && p2.MustCut)
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
        else if (_hasLitCLR)
        {
            _expectedCuts.Clear();
            _expectedCuts.AddRange(_wireInfos.OrderByDescending(w => w.ConnectedFrom));
            return;
        }
        else if (serialFirstNumeral != null && (curIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == serialFirstNumeral.Value - '0')) != -1)
        {
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
                    i < 3 ? null : _wireInfos[(curIndex + _wireInfos.Count - 3) % _wireInfos.Count],
                    i < 2 ? null : _wireInfos[(curIndex + _wireInfos.Count - 2) % _wireInfos.Count],
                    _wireInfos[(curIndex + _wireInfos.Count - 1) % _wireInfos.Count],
                    _wireInfos[curIndex]
                );
            }

            if (_wireInfos[curIndex].MustCut)
                _expectedCuts.Add(_wireInfos[curIndex]);

            curIndex = (curIndex + 1) % _wireInfos.Count;
            curStep = (curStep + rules.Length + (reverse ? -1 : 1)) % rules.Length;
        }

        Debug.Log("[FollowTheLeader] My expectation (" + _expectedCuts.Count + "): cut " + string.Join(", ", _expectedCuts.Select(wi => string.Format("{0}-to-{1} ({2})", wi.ConnectedFrom + 1, wi.ConnectedTo + 1, ColorNames[wi.Color])).ToArray()));
    }
}

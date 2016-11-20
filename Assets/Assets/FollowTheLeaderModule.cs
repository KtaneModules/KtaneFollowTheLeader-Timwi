using System;
using System.Collections.Generic;
using System.Linq;
using FollowTheLeader;
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

    enum Color { Red, Green, White, Yellow, Blue, Black }

    class WireInfo
    {
        // 0–11
        public int ConnectedFrom;
        public int ConnectedTo;
        public bool DoesSkip { get { return ConnectedTo != (ConnectedFrom + 1) % 12; } }

        public Color Color;
        public bool MustCut;
        public bool IsCut;
        public string Justification;
        public KMSelectable Selectable;

        public void Activate(Transform transform, FollowTheLeaderModule module)
        {
            Selectable = transform.GetComponent<KMSelectable>();

            // The irony of using one RNG to seed another RNG isn’t lost on me
            var seed = Rnd.Range(0, int.MaxValue);
            var rnd = new System.Random(seed);
            var lengthIndex = DoesSkip ? ConnectedFrom % 2 : 2;

            transform.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) Color, MeshGenerator.WirePiece.Uncut, false);
            transform.GetComponent<MeshRenderer>().material = module.ColorMaterials[(int) Color];

            rnd = new System.Random(seed);
            var wireHighlight = transform.Find(string.Format("Wire {0}-to-{1} highlight", ConnectedFrom + 1, ConnectedTo + 1));
            var highlightMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) Color, MeshGenerator.WirePiece.Uncut, true);
            wireHighlight.GetComponent<MeshFilter>().mesh = highlightMesh;

            var clone = wireHighlight.Find("Highlight(Clone)");
            if (clone != null)
                clone.GetComponent<MeshFilter>().mesh = highlightMesh;

            rnd = new System.Random(seed);
            var cutMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) Color, MeshGenerator.WirePiece.Cut, false);
            rnd = new System.Random(seed);
            var cutMeshHighlight = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) Color, MeshGenerator.WirePiece.Cut, true);
            rnd = new System.Random(seed);
            var copperMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) Color, MeshGenerator.WirePiece.Copper, false);

            IsCut = false;
            transform.GetComponent<KMSelectable>().OnInteract += delegate
            {
                if (IsCut)
                {
                    Debug.LogFormat("[FollowTheLeader] You tried to cut a wire that is already cut ({0}).", this);
                    return false;
                }

                Selectable.AddInteractionPunch();
                module.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, transform);

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
                child.AddComponent<MeshRenderer>().material = module.CopperMaterial;

                if (module._expectedCuts.Count == 0 || module._expectedCuts[0] != this)
                {
                    Debug.LogFormat("[FollowTheLeader] Strike because you cut {0} but I expected {1}.", this, module._expectedCuts.Count == 0 ? "no more wires to be cut" : module._expectedCuts[0].ToString());
                    module.Module.HandleStrike();
                }
                else
                {
                    while (module._expectedCuts.Count > 0 && module._expectedCuts[0].IsCut)
                        module._expectedCuts.RemoveAt(0);
                    Debug.LogFormat("[FollowTheLeader] Cutting {0} was correct. Expectation now is{1}", this, module._expectedCuts.Count == 0 ? " that you’re done." : ":\n" + string.Join("\n", module._expectedCuts.Select(wi => wi.ToString()).ToArray()));
                    if (module._expectedCuts.Count == 0)
                        module.Module.HandlePass();
                }
                return false;
            };
            transform.gameObject.SetActive(true);
        }

        public override string ToString()
        {
            return string.Format("Wire {0}-to-{1} ({2})", ConnectedFrom + 1, ConnectedTo + 1, Color.ToString());
        }

        public string ToStringFull()
        {
            return ToString() + "; " + Justification;
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
                Color = (Color) Rnd.Range(0, 6)
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
            _wireInfos[i].Activate(Module.transform.Find(string.Format("Wire {0}-to-{1}", _wireInfos[i].ConnectedFrom + 1, _wireInfos[i].ConnectedTo + 1)), this);

        // Code from sircharles
        Selectable.Children = _wireInfos.OrderBy(wi => wi.ConnectedFrom).Select(wi => wi.Selectable).ToArray();
        Selectable.ChildRowLength = Selectable.Children.Length;
        var modSelectable = GetComponent("ModSelectable");
        if (modSelectable != null)
            modSelectable.GetType().GetMethod("CopySettingsFromProxy").Invoke(modSelectable, null);
    }

    sealed class RuleInfo
    {
        public string Name;
        public string Formulation;
        public Func<WireInfo, WireInfo, WireInfo, WireInfo, bool> Function;
    }

    static Color[] _whiteBlack = new[] { Color.White, Color.Black };
    static RuleInfo[] rules = Ex.NewArray(
        new RuleInfo
        {
            Name = "A or N",
            Formulation = "the previous wire is not yellow or blue or green",
            Function = (p3, p2, p1, p0) => !new[] { Color.Yellow, Color.Blue, Color.Green }.Contains(p1.Color)
        },
        new RuleInfo
        {
            Name = "B or O",
            Formulation = "the previous wire leads to an even numbered plug",
            Function = (p3, p2, p1, p0) => p1.ConnectedTo % 2 == 1
        },
        new RuleInfo
        {
            Name = "C or P",
            Formulation = "the previous wire should be cut",
            Function = (p3, p2, p1, p0) => p1.MustCut
        },
        new RuleInfo
        {
            Name = "D or Q",
            Formulation = "the previous wire is red or blue or black",
            Function = (p3, p2, p1, p0) => new[] { Color.Red, Color.Blue, Color.Black }.Contains(p1.Color)
        },
        new RuleInfo
        {
            Name = "E or R",
            Formulation = "two of the previous three wires shared a color",
            Function = (p3, p2, p1, p0) => p1.Color == p2.Color || p1.Color == p3.Color || p2.Color == p3.Color,
        },
        new RuleInfo
        {
            Name = "F or S",
            Formulation = "exactly one of the previous two wires were the same color as this wire",
            Function = (p3, p2, p1, p0) => (p0.Color == p1.Color) ^ (p0.Color == p2.Color),
        },
        new RuleInfo
        {
            Name = "G or T",
            Formulation = "the previous wire is yellow or white or green",
            Function = (p3, p2, p1, p0) => new[] { Color.Yellow, Color.White, Color.Green }.Contains(p1.Color)
        },
        new RuleInfo
        {
            Name = "H or U",
            Formulation = "the previous wire should not be cut",
            Function = (p3, p2, p1, p0) => !p1.MustCut
        },
        new RuleInfo
        {
            Name = "I or V",
            Formulation = "the previous wire skips a plug",
            Function = (p3, p2, p1, p0) => p1.DoesSkip
        },
        new RuleInfo
        {
            Name = "J or W",
            Formulation = "the previous wire is not white or black or red",
            Function = (p3, p2, p1, p0) => !new[] { Color.White, Color.Black, Color.Red }.Contains(p1.Color)
        },
        new RuleInfo
        {
            Name = "K or X",
            Formulation = "the previous two wires are different colors",
            Function = (p3, p2, p1, p0) => p1.Color != p2.Color,
        },
        new RuleInfo
        {
            Name = "L or Y",
            Formulation = "the previous wire does not lead to a position labeled 6 or less",
            Function = (p3, p2, p1, p0) => p1.ConnectedTo > 5,
        },
        new RuleInfo
        {
            Name = "M or Z",
            Formulation = "exactly one or neither of the previous two wires are white or black",
            Function = (p3, p2, p1, p0) => !(_whiteBlack.Contains(p1.Color) && _whiteBlack.Contains(p2.Color))
        }
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
        int startIndex;
        var serialFirstNumeral = _serial.Where(ch => ch >= '0' && ch <= '9').FirstOrNull();
        if (_hasRJ && (startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 3 && wi.ConnectedTo == 4)) != -1)
        {
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == _numBatteries)) != -1)
        {
        }
        else if (serialFirstNumeral != null && (startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == serialFirstNumeral.Value - '0')) != -1)
        {
        }
        else if (_hasLitCLR)
        {
            Debug.Log("[FollowTheLeader] CLR rule: cut everything in reverse order.");
            foreach (var wi in _wireInfos)
                wi.Justification = "CLR rule.";
            _expectedCuts.Clear();
            _expectedCuts.AddRange(_wireInfos.OrderByDescending(w => w.ConnectedFrom));
            goto end;
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 0)) != -1)
        {
        }
        else
            startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 1);

        Debug.Log("[FollowTheLeader] Starting at wire: " + _wireInfos[startIndex]);

        var curIndex = startIndex;
        // The starting step corresponds to the first letter in the serial number.
        var curStep = _serial.Where(ch => ch >= 'A' && ch <= 'Z').Select(ch => ch - 'A').FirstOrDefault() % rules.Length;
        // If the wire at the starting plug is red, green, or white, progress through the steps in reverse alphabetical order instead.
        var reverse = new[] { Color.Red, Color.Green, Color.White }.Contains(_wireInfos[curIndex].Color);

        _expectedCuts.Clear();

        // Finally, determine which wires need cutting
        for (int i = 0; i < _wireInfos.Count; i++)
        {
            if (i == 0)
            {
                // Always cut the first one.
                _wireInfos[curIndex].MustCut = true;
                _wireInfos[curIndex].Justification = "Cut because this is the starting wire.";
            }
            else
            {
                _wireInfos[curIndex].MustCut = rules[curStep].Function(
                    _wireInfos[(curIndex + _wireInfos.Count - 3) % _wireInfos.Count],
                    _wireInfos[(curIndex + _wireInfos.Count - 2) % _wireInfos.Count],
                    _wireInfos[(curIndex + _wireInfos.Count - 1) % _wireInfos.Count],
                    _wireInfos[curIndex]
                );
                _wireInfos[curIndex].Justification = string.Format("Rule {0}: cut if {1} ⇒ {2}", rules[curStep].Name, rules[curStep].Formulation, _wireInfos[curIndex].MustCut ? "CUT" : "DON’T CUT");
                curStep = (curStep + rules.Length + (reverse ? -1 : 1)) % rules.Length;
            }

            if (_wireInfos[curIndex].MustCut)
                _expectedCuts.Add(_wireInfos[curIndex]);

            curIndex = (curIndex + 1) % _wireInfos.Count;
        }

        Debug.Log("[FollowTheLeader] Wire state:\n" + string.Join("\n", Enumerable.Range(0, _wireInfos.Count).Select(i => _wireInfos[(i + startIndex) % _wireInfos.Count].ToStringFull()).ToArray()));
        end:
        Debug.Log("[FollowTheLeader] Expectation:\n" + string.Join("\n", _expectedCuts.Select(wi => wi.ToString()).ToArray()));
    }
}

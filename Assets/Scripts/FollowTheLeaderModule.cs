using System;
using System.Collections.Generic;
using System.Linq;
using FollowTheLeader;
using Newtonsoft.Json;
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
    public TextMesh TextTemplObj;

    enum WireColor { Red, Green, White, Yellow, Blue, Black }

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    class WireInfo
    {
        // 0–11
        public int ConnectedFrom;
        public int ConnectedTo;
        public bool DoesSkip { get { return ConnectedTo != (ConnectedFrom + 1) % 12; } }

        public WireColor WireColor;
        public bool MustCut;
        public bool IsCut;
        public string Justification;
        public KMSelectable Selectable;

        private static Color Lightness(Color col, float lgh)
        {
            float h, s, v;
            Color.RGBToHSV(col, out h, out s, out v);
            return Color.HSVToRGB(h, s, lgh);
        }
        private static readonly Dictionary<WireColor, Color> _colorDic = new Dictionary<WireColor, Color>
        {
            { WireColor.Black, Lightness( new Color(0x29 / 255f, 0x29 / 255f, 0x29 / 255f),.1f) },
            { WireColor.Blue, Lightness(new Color(0x17 / 255f, 0x78 / 255f, 0xFF / 255f) ,.5f)},
            { WireColor.Green, Lightness(new Color(0x00 / 255f, 0xAE / 255f, 0x00 / 255f),.8f) },
            { WireColor.Red, Lightness(new Color(0xEA / 255f, 0x00 / 255f, 0x00 / 255f) ,1f)},
            { WireColor.White, Lightness(new Color(0xE8 / 255f, 0xE8 / 255f, 0xE8 / 255f),.8f )},
            { WireColor.Yellow, Lightness(new Color(0xFF / 255f, 0xFF / 255f, 0x15 / 255f),.8f) }
        };

        public void Activate(Transform transform, FollowTheLeaderModule module, TextMesh colorBlindTextTemplObj)
        {
            Selectable = transform.GetComponent<KMSelectable>();

            // The irony of using one RNG to seed another RNG isn’t lost on me
            var seed = Rnd.Range(0, int.MaxValue);
            var rnd = new System.Random(seed);
            var lengthIndex = DoesSkip ? ConnectedFrom % 2 : 2;

            transform.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Uncut, false);
            transform.GetComponent<MeshRenderer>().material = module.ColorMaterials[(int) WireColor];

            rnd = new System.Random(seed);
            var wireHighlight = transform.Find(string.Format("Wire {0}-to-{1} highlight", ConnectedFrom + 1, ConnectedTo + 1));
            var highlightMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Uncut, true);
            wireHighlight.GetComponent<MeshFilter>().mesh = highlightMesh;

            var clone = wireHighlight.Find("Highlight(Clone)");
            if (clone != null)
                clone.GetComponent<MeshFilter>().mesh = highlightMesh;

            rnd = new System.Random(seed);
            var cutMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Cut, false);
            rnd = new System.Random(seed);
            var cutMeshHighlight = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Cut, true);
            rnd = new System.Random(seed);
            var copperMesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Copper, false);

            IsCut = false;
            Selectable.OnInteract += delegate
            {
                if (IsCut)
                {
                    Debug.LogFormat("[FollowTheLeader #{1}] You tried to cut a wire that is already cut ({0}).", this, module._moduleId);
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
                    Debug.LogFormat("[FollowTheLeader #{2}] Strike because you cut {0} but I expected {1}.", this, module._expectedCuts.Count == 0 ? "no more wires to be cut" : module._expectedCuts[0].ToString(), module._moduleId);
                    module.Module.HandleStrike();
                }
                else
                {
                    while (module._expectedCuts.Count > 0 && module._expectedCuts[0].IsCut)
                        module._expectedCuts.RemoveAt(0);
                    Debug.LogFormat("[FollowTheLeader #{2}] Cutting {0} was correct. Expectation now is{1}", this, module._expectedCuts.Count == 0 ? " that you’re done." : ":\n" + string.Join("\n", module._expectedCuts.Select(wi => wi.ToString()).ToArray()), module._moduleId);
                    if (module._expectedCuts.Count == 0)
                        module.Module.HandlePass();
                }
                return false;
            };
            transform.gameObject.SetActive(true);

            if (colorBlindTextTemplObj != null)
            {
                var obj = Instantiate(colorBlindTextTemplObj);
                obj.transform.parent = transform;
                obj.transform.localPosition = new Vector3((float) (MeshGenerator.GetWireLength(lengthIndex) / 2), -.002f, .0125f);
                obj.transform.localRotation = Quaternion.Euler(0, 0, 0);
                obj.transform.localScale = new Vector3(.001f, -.001f, .001f);
                obj.text = WireColor.ToString().ToUpperInvariant();
                obj.color = _colorDic[WireColor];
                obj.gameObject.SetActive(true);
            }
        }

        public override string ToString()
        {
            return string.Format("Wire {0}-to-{1} ({2})", ConnectedFrom + 1, ConnectedTo + 1, WireColor.ToString());
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
        _moduleId = _moduleIdCounter++;
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
                WireColor = (WireColor) Rnd.Range(0, 6)
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

        // Disable all the wires (we will later re-enable the ones we need)
        for (int from = 0; from < 12; from++)
            foreach (var skip in new[] { false, true })
                Module.transform.Find(string.Format("Wire {0}-to-{1}", from + 1, (from + (skip ? 2 : 1)) % 12 + 1)).gameObject.SetActive(false);

        var colorblindMode = GetComponent<KMColorblindMode>().ColorblindModeActive;
        for (int i = 0; i < _wireInfos.Count; i++)
            _wireInfos[i].Activate(Module.transform.Find(string.Format("Wire {0}-to-{1}", _wireInfos[i].ConnectedFrom + 1, _wireInfos[i].ConnectedTo + 1)), this, colorblindMode ? TextTemplObj : null);

        Selectable.Children = _wireInfos.OrderBy(wi => wi.ConnectedFrom).Select(wi => wi.Selectable).ToArray();
        Selectable.ChildRowLength = Selectable.Children.Length;
        Selectable.UpdateChildren();
    }

    sealed class RuleInfo
    {
        public string Name;
        public string Formulation;
        public Func<WireInfo, WireInfo, WireInfo, WireInfo, bool> Function;
    }

    static WireColor[] _whiteBlack = new[] { WireColor.White, WireColor.Black };
    static RuleInfo[] rules = Ex.NewArray(
        new RuleInfo
        {
            Name = "A or N",
            Formulation = "the previous wire is not yellow or blue or green",
            Function = (p3, p2, p1, p0) => !new[] { WireColor.Yellow, WireColor.Blue, WireColor.Green }.Contains(p1.WireColor)
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
            Function = (p3, p2, p1, p0) => new[] { WireColor.Red, WireColor.Blue, WireColor.Black }.Contains(p1.WireColor)
        },
        new RuleInfo
        {
            Name = "E or R",
            Formulation = "two of the previous three wires shared a color",
            Function = (p3, p2, p1, p0) => p1.WireColor == p2.WireColor || p1.WireColor == p3.WireColor || p2.WireColor == p3.WireColor,
        },
        new RuleInfo
        {
            Name = "F or S",
            Formulation = "exactly one of the previous two wires were the same color as this wire",
            Function = (p3, p2, p1, p0) => (p0.WireColor == p1.WireColor) ^ (p0.WireColor == p2.WireColor),
        },
        new RuleInfo
        {
            Name = "G or T",
            Formulation = "the previous wire is yellow or white or green",
            Function = (p3, p2, p1, p0) => new[] { WireColor.Yellow, WireColor.White, WireColor.Green }.Contains(p1.WireColor)
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
            Function = (p3, p2, p1, p0) => !new[] { WireColor.White, WireColor.Black, WireColor.Red }.Contains(p1.WireColor)
        },
        new RuleInfo
        {
            Name = "K or X",
            Formulation = "the previous two wires are different colors",
            Function = (p3, p2, p1, p0) => p1.WireColor != p2.WireColor,
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
            Function = (p3, p2, p1, p0) => !(_whiteBlack.Contains(p1.WireColor) && _whiteBlack.Contains(p2.WireColor))
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

        // Figure out the starting wire (as index into wireInfos, rather than peg number)
        int startIndex;
        var serialFirstNumeral = _serial.Where(ch => ch >= '0' && ch <= '9').FirstOrNull();
        if (_hasRJ && (startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 3 && wi.ConnectedTo == 4)) != -1)
        {
            Debug.LogFormat("[FollowTheLeader #{0}] Starting at wire {1} because RJ port.", _moduleId, _wireInfos[startIndex]);
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == _numBatteries)) != -1)
        {
            Debug.LogFormat("[FollowTheLeader #{0}] Starting at wire {1} because number of batteries.", _moduleId, _wireInfos[startIndex]);
        }
        else if (serialFirstNumeral != null && (startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom + 1 == serialFirstNumeral.Value - '0')) != -1)
        {
            Debug.LogFormat("[FollowTheLeader #{0}] Starting at wire {1} because serial number’s first numeral.", _moduleId, _wireInfos[startIndex]);
        }
        else if (_hasLitCLR)
        {
            Debug.Log("[FollowTheLeader] Cut everything in reverse order because lit CLR.");
            foreach (var wi in _wireInfos)
                wi.Justification = "CLR rule.";
            _expectedCuts.Clear();
            _expectedCuts.AddRange(_wireInfos.OrderByDescending(w => w.ConnectedFrom));
            goto end;
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 0)) != -1)
        {
            Debug.LogFormat("[FollowTheLeader #{0}] Starting at wire {1} because no other rule applies.", _moduleId, _wireInfos[startIndex]);
        }
        else
        {
            startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == 1);
            Debug.LogFormat("[FollowTheLeader #{0}] Starting at wire {1} because no other rule applies.", _moduleId, _wireInfos[startIndex]);
        }

        var curIndex = startIndex;
        // The starting step corresponds to the first letter in the serial number.
        var curStep = _serial.Where(ch => ch >= 'A' && ch <= 'Z').Select(ch => ch - 'A').FirstOrDefault() % rules.Length;
        // If the wire at the starting plug is red, green, or white, progress through the steps in reverse alphabetical order instead.
        var reverse = new[] { WireColor.Red, WireColor.Green, WireColor.White }.Contains(_wireInfos[curIndex].WireColor);

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

        Debug.LogFormat("[FollowTheLeader #{0}] Wire state:\n{1}", _moduleId, string.Join("\n", Enumerable.Range(0, _wireInfos.Count).Select(i => _wireInfos[(i + startIndex) % _wireInfos.Count].ToStringFull()).ToArray()));
        end:
        Debug.LogFormat("[FollowTheLeader #{0}] Expectation:\n{1}", _moduleId, string.Join("\n", _expectedCuts.Select(wi => wi.ToString()).ToArray()));
    }

#pragma warning disable 414
    private string TwitchHelpMessage = @"Cut the wires in a specific order with “!{0} cut 4 6 9 10 1 2”.";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        if (!command.StartsWith("cut ", StringComparison.OrdinalIgnoreCase))
            return null;
        command = command.Substring(4);
        var wires = new List<KMSelectable>();
        foreach (var cmd in command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int number;
            if (!int.TryParse(cmd, out number))
                return null;
            var wireInfo = _wireInfos.FirstOrDefault(wi => wi.ConnectedFrom + 1 == number);
            if (wireInfo == null)
                return null;
            wires.Add(wireInfo.Selectable);
        }
        return wires.ToArray();
    }
}

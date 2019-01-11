using System;
using System.Collections.Generic;
using System.Linq;
using FollowTheLeader;
using KModkit;
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
    public KMRuleSeedable RuleSeedable;
    public KMColorblindMode ColorblindMode;

    public Material[] ColorMaterials;
    public Material CopperMaterial;
    public Mesh[] WireColliderMeshes;
    public TextMesh TextTemplObj;

    enum WireColor { Red, Green, White, Yellow, Blue, Black }

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    class WireInfo
    {
        // 1–12
        public int ConnectedFrom;
        public int ConnectedTo;
        public bool DoesSkip { get { return ConnectedTo != ConnectedFrom % 12 + 1; } }

        public WireColor WireColor;
        public bool MustCut;
        public bool IsCut;
        public string Justification;
        public KMSelectable Selectable;
        private TextMesh _colorBlindIndicator;

        private static Color Lightness(Color col, float lgh)
        {
            float h, s, v;
            Color.RGBToHSV(col, out h, out s, out v);
            return Color.HSVToRGB(h, s, lgh);
        }
        private static readonly Dictionary<WireColor, Color> _colorDic = new Dictionary<WireColor, Color>
        {
            { WireColor.Black, Lightness(new Color(0x29 / 255f, 0x29 / 255f, 0x29 / 255f), .1f) },
            { WireColor.Blue, Lightness(new Color(0x17 / 255f, 0x78 / 255f, 0xFF / 255f), .5f) },
            { WireColor.Green, Lightness(new Color(0x00 / 255f, 0xAE / 255f, 0x00 / 255f), .8f) },
            { WireColor.Red, Lightness(new Color(0xEA / 255f, 0x00 / 255f, 0x00 / 255f), 1f) },
            { WireColor.White, Lightness(new Color(0xE8 / 255f, 0xE8 / 255f, 0xE8 / 255f), .8f) },
            { WireColor.Yellow, Lightness(new Color(0xFF / 255f, 0xFF / 255f, 0x15 / 255f), .8f) }
        };

        public void Activate(Transform transform, FollowTheLeaderModule module, TextMesh colorBlindTextTemplObj, bool colorBlindMode)
        {
            Selectable = transform.GetComponent<KMSelectable>();

            // The irony of using one RNG to seed another RNG isn’t lost on me
            var seed = Rnd.Range(0, int.MaxValue);
            var rnd = new System.Random(seed);
            var lengthIndex = DoesSkip ? 1 - ConnectedFrom % 2 : 2;

            transform.GetComponent<MeshFilter>().mesh = MeshGenerator.GenerateWire(rnd, lengthIndex, (int) WireColor, MeshGenerator.WirePiece.Uncut, false);
            transform.GetComponent<MeshRenderer>().material = module.ColorMaterials[(int) WireColor];

            rnd = new System.Random(seed);
            var wireHighlight = transform.Find(string.Format("Wire {0}-to-{1} highlight", ConnectedFrom, ConnectedTo));
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
                    return false;

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
                    Debug.LogFormat("[Follow the Leader #{2}] Strike because you cut {0} but I expected {1}.", this, module._expectedCuts.Count == 0 ? "no more wires to be cut" : module._expectedCuts[0].ToString(), module._moduleId);
                    module.Module.HandleStrike();
                }
                else
                {
                    while (module._expectedCuts.Count > 0 && module._expectedCuts[0].IsCut)
                        module._expectedCuts.RemoveAt(0);
                    if (module._expectedCuts.Count == 0)
                    {
                        Debug.LogFormat("[Follow the Leader #{0}] Cutting {1} was correct. Module solved.", module._moduleId, this);
                        module.Module.HandlePass();
                    }
                    else
                        Debug.LogFormat("[Follow the Leader #{0}] Cutting {1} was correct. Next expected wire: {2}", module._moduleId, this, module._expectedCuts[0]);
                }
                return false;
            };
            transform.gameObject.SetActive(true);

            _colorBlindIndicator = Instantiate(colorBlindTextTemplObj);
            _colorBlindIndicator.transform.parent = transform;
            _colorBlindIndicator.transform.localPosition = new Vector3((float) (MeshGenerator.GetWireLength(lengthIndex) / 2), -.002f, .0125f);
            _colorBlindIndicator.transform.localRotation = Quaternion.Euler(0, 0, 0);
            _colorBlindIndicator.transform.localScale = new Vector3(.001f, -.001f, .001f);
            _colorBlindIndicator.text = WireColor.ToString().ToUpperInvariant();
            _colorBlindIndicator.color = _colorDic[WireColor];
            _colorBlindIndicator.gameObject.SetActive(colorBlindMode);
        }

        public void EnableColorBlindMode()
        {
            _colorBlindIndicator.gameObject.SetActive(true);
        }

        public override string ToString()
        {
            return string.Format("Wire {0}-to-{1} ({2})", ConnectedFrom, ConnectedTo, WireColor.ToString());
        }

        public string ToStringFull()
        {
            return ToString() + "; " + Justification;
        }
    }

    private List<WireInfo> _wireInfos;
    private List<WireInfo> _expectedCuts;

    delegate bool EvaluateRule(Func<int, WireInfo> getWire, int wireIndex, int numWires, IEnumerable<WireInfo> wiresSinceStart);

    delegate RuleInfo MakeSimpleRule(string name);
    delegate RuleInfo MakeNumberRule6(string name, int number);
    delegate RuleInfo MakeNumberRule12(string name, int number);
    delegate RuleInfo MakeColorRule(string name, WireColor[] colors);

    sealed class RuleInfo
    {
        public string Name;
        public string Phrasing;
        public EvaluateRule Evaluate;
    }

    private static int[] _primes = { 2, 3, 5, 7, 11 };
    private static readonly Delegate[] _tableRules = Ex.NewArray<Delegate>(
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Cut this wire.", Evaluate = (w, ix, num, prev) => true }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Don’t cut this wire.", Evaluate = (w, ix, num, prev) => false }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire leads to an even numbered plug.", Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom % 2 == 0 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire leads to an odd numbered plug.", Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom % 2 != 0 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire leads to a prime numbered plug.", Evaluate = (w, ix, num, prev) => _primes.Contains(w(ix).ConnectedFrom) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire does not lead to a prime numbered plug.", Evaluate = (w, ix, num, prev) => !_primes.Contains(w(ix).ConnectedFrom) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire should be cut.", Evaluate = (w, ix, num, prev) => w(ix - 1).MustCut }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire should not be cut.", Evaluate = (w, ix, num, prev) => !w(ix - 1).MustCut }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous two wires are the same color.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor == w(ix - 1).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous two wires are different colors.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor != w(ix - 1).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Two of the previous three wires share a color.", Evaluate = (w, ix, num, prev) => Enumerable.Range(-3, 3).Select(off => w(ix + off)).Distinct().Count() < 3 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous three wires have different colors.", Evaluate = (w, ix, num, prev) => Enumerable.Range(-3, 3).Select(off => w(ix + off)).Distinct().Count() == 3 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous three wires are all the same color.", Evaluate = (w, ix, num, prev) => Enumerable.Range(-3, 3).Select(off => w(ix + off)).Distinct().Count() == 1 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire has the same color as this wire.", Evaluate = (w, ix, num, prev) => w(ix - 1).WireColor == w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire has a different color than this wire.", Evaluate = (w, ix, num, prev) => w(ix - 1).WireColor != w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Neither of the previous two wires is the same color as this wire.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor != w(ix).WireColor && w(ix - 1).WireColor != w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one of the previous two wires is the same color as this wire.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor == w(ix).WireColor ^ w(ix - 1).WireColor == w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Both of the previous two wires are the same color as this wire.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor == w(ix).WireColor && w(ix - 1).WireColor == w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one or neither of the previous two wires is the same color as this wire.", Evaluate = (w, ix, num, prev) => !(w(ix - 2).WireColor == w(ix).WireColor && w(ix - 1).WireColor == w(ix).WireColor) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one or both of the previous two wires are the same color as this wire.", Evaluate = (w, ix, num, prev) => w(ix - 2).WireColor == w(ix).WireColor || w(ix - 1).WireColor == w(ix).WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Neither or both of the previous two wires is the same color as this wire.", Evaluate = (w, ix, num, prev) => !(w(ix - 2).WireColor == w(ix).WireColor ^ w(ix - 1).WireColor == w(ix).WireColor) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire skips a plug.", Evaluate = (w, ix, num, prev) => w(ix - 1).DoesSkip }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire does not skip a plug.", Evaluate = (w, ix, num, prev) => !w(ix - 1).DoesSkip }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Neither of the previous two wires skips a plug.", Evaluate = (w, ix, num, prev) => !w(ix - 2).DoesSkip && !w(ix - 1).DoesSkip }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one of the previous two wires skips a plug.", Evaluate = (w, ix, num, prev) => w(ix - 2).DoesSkip ^ w(ix - 1).DoesSkip }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Both of the previous two wires skip a plug.", Evaluate = (w, ix, num, prev) => w(ix - 2).DoesSkip && w(ix - 1).DoesSkip }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one or neither of the previous two wires skips a plug.", Evaluate = (w, ix, num, prev) => !(w(ix - 2).DoesSkip && w(ix - 1).DoesSkip) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Exactly one or both of the previous two wires skip a plug.", Evaluate = (w, ix, num, prev) => w(ix - 2).DoesSkip || w(ix - 1).DoesSkip }),
        new MakeNumberRule12((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire leads to a position labeled {0} or less.", n), Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom <= n }),
        new MakeNumberRule12((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire does not lead to a position labeled {0} or less.", n), Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom > n }),
        new MakeNumberRule12((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire leads to a position labeled {0} or more.", n), Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom >= n }),
        new MakeNumberRule12((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire does not lead to a position labeled {0} or more.", n), Evaluate = (w, ix, num, prev) => w(ix).ConnectedFrom < n }),
        new MakeNumberRule6((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("There are {0} or more wires on the module in total.", n), Evaluate = (w, ix, num, prev) => num >= n }),
        new MakeNumberRule6((name, n) => new RuleInfo { Name = name, Phrasing = string.Format("There are {0} or fewer wires on the module in total.", n), Evaluate = (w, ix, num, prev) => num <= n }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire is {0} or {1} or {2}.", cs[0], cs[1], cs[2]), Evaluate = (w, ix, num, prev) => cs.Take(3).Contains(w(ix - 1).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("The previous wire is not {0} or {1} or {2}.", cs[0], cs[1], cs[2]), Evaluate = (w, ix, num, prev) => !cs.Take(3).Contains(w(ix - 1).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("The wire before the previous is {0} or {1} or {2}.", cs[0], cs[1], cs[2]), Evaluate = (w, ix, num, prev) => cs.Take(3).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("The wire before the previous is not {0} or {1} or {2}.", cs[0], cs[1], cs[2]), Evaluate = (w, ix, num, prev) => !cs.Take(3).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Neither of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => !cs.Take(2).Contains(w(ix - 1).WireColor) && !cs.Take(2).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Exactly one of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => cs.Take(2).Contains(w(ix - 1).WireColor) ^ cs.Take(2).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Both of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => cs.Take(2).Contains(w(ix - 1).WireColor) && cs.Take(2).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Exactly one or neither of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => !cs.Take(2).Contains(w(ix - 1).WireColor) || !cs.Take(2).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Exactly one or both of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => cs.Take(2).Contains(w(ix - 1).WireColor) || cs.Take(2).Contains(w(ix - 2).WireColor) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("Neither or both of the previous two wires are {0} or {1}.", cs[0], cs[1]), Evaluate = (w, ix, num, prev) => !(cs.Take(2).Contains(w(ix - 1).WireColor) ^ cs.Take(2).Contains(w(ix - 2).WireColor)) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("All previous {0} wires after the starting wire should be cut.", cs[0]), Evaluate = (_, ix, num, prev) => prev.Skip(1).All(w => w.MustCut || w.WireColor != cs[0]) }),
        new MakeColorRule((name, cs) => new RuleInfo { Name = name, Phrasing = string.Format("All previous {0} wires after the starting wire should not be cut.", cs[0]), Evaluate = (_, ix, num, prev) => prev.Skip(1).All(w => !w.MustCut || w.WireColor != cs[0]) }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "More than half of the wires so far (including the starting wire) should be cut.", Evaluate = (_, ix, num, prev) => prev.Count(w => w.MustCut) > prev.Count() * .5 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Less than half of the wires so far (including the starting wire) should be cut.", Evaluate = (_, ix, num, prev) => prev.Count(w => w.MustCut) < prev.Count() * .5 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "More than half of the wires so far (not including the starting wire) should be cut.", Evaluate = (_, ix, num, prev) => prev.Skip(1).Count(w => w.MustCut) > (prev.Count() - 1) * .5 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "Less than half of the wires so far (not including the starting wire) should be cut.", Evaluate = (_, ix, num, prev) => prev.Skip(1).Count(w => w.MustCut) < (prev.Count() - 1) * .5 }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire has the same color as the starting wire.", Evaluate = (w, ix, num, prev) => w(ix - 1).WireColor == prev.First().WireColor }),
        new MakeSimpleRule(name => new RuleInfo { Name = name, Phrasing = "The previous wire has a different color than the starting wire.", Evaluate = (w, ix, num, prev) => w(ix - 1).WireColor != prev.First().WireColor })
    );

    private static readonly string[] _rule4Indicators = "TRN,FRK,FRQ,BOB,IND,CAR,SIG,NSA,SND,CLR,MSA".Split(',');

    void Start()
    {
        _moduleId = _moduleIdCounter++;


        // STEP 1: DECIDE WHICH WIRES SKIP WHICH PLUGS
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
                ConnectedFrom = currentPeg + 1,
                ConnectedTo = nextPeg + 1,
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
            _wireInfos[i].ConnectedFrom = (_wireInfos[i].ConnectedFrom + rotation - 1) % 12 + 1;
            _wireInfos[i].ConnectedTo = (_wireInfos[i].ConnectedTo + rotation - 1) % 12 + 1;
        }

        // Disable all the wires
        foreach (var skip in new[] { false, true })
            for (int from = 0; from < 12; from++)
                Module.transform.Find(string.Format("Wire {0}-to-{1}", from + 1, (from + (skip ? 2 : 1)) % 12 + 1)).gameObject.SetActive(false);

        // Re-enable the wires we need and set up their click handler and color-blind mode
        var colorblindMode = ColorblindMode.ColorblindModeActive;
        for (int i = 0; i < _wireInfos.Count; i++)
            _wireInfos[i].Activate(Module.transform.Find(string.Format("Wire {0}-to-{1}", _wireInfos[i].ConnectedFrom, _wireInfos[i].ConnectedTo)), this, TextTemplObj, colorblindMode);

        Selectable.Children = _wireInfos.OrderBy(wi => wi.ConnectedFrom).Select(wi => wi.Selectable).ToArray();
        Selectable.ChildRowLength = Selectable.Children.Length;
        Selectable.UpdateChildren();


        // STEP 2: GENERATE THE RULES (KMRuleSeedable)
        var rnd = RuleSeedable.GetRNG();

        var ports = new[] { Port.Parallel, Port.Serial, Port.RJ45, Port.PS2, Port.DVI, Port.StereoRCA };
        var hasRule1Port = Bomb.IsPortPresent(ports[rnd.Next(0, ports.Length)]);

        var snLetters = Bomb.GetSerialNumberLetters().ToArray();
        var snDigits = Bomb.GetSerialNumberNumbers().ToArray();
        var startingPlugConditions = Ex.NewArray(
            Bomb.GetIndicators().Count(),
            Bomb.GetOnIndicators().Count(),
            Bomb.GetOffIndicators().Count(),
            Bomb.GetBatteryCount(),
            Bomb.GetBatteryHolderCount(),
            Bomb.GetBatteryCount(Battery.AA) + Bomb.GetBatteryCount(Battery.AAx3) + Bomb.GetBatteryCount(Battery.AAx4),
            Bomb.GetBatteryCount(Battery.D),
            Bomb.GetPortCount(),
            Bomb.GetPortPlateCount(),
            Bomb.CountUniquePorts(),
            Bomb.GetModuleNames().Count,
            Bomb.GetSolvableModuleNames().Count,
            snLetters[0] - 'A' + 1,
            snLetters[1] - 'A' + 1,
            snLetters[snLetters.Length - 1] - 'A' + 1,
            snLetters[snLetters.Length - 2] - 'A' + 1,
            snDigits[snDigits.Length - 1],
            snDigits[snDigits.Length - 2],
            snDigits[0]);

        var rule2ConditionIx = rnd.Next(0, startingPlugConditions.Length);
        var rule3ConditionIx = rnd.Next(0, startingPlugConditions.Length - 1);
        if (rule3ConditionIx >= rule2ConditionIx)
            rule3ConditionIx++;
        var rule2Plug = startingPlugConditions[rule2ConditionIx];
        var rule3Plug = startingPlugConditions[rule3ConditionIx];

        var rule1StartPlug = rnd.Next(1, 13);
        var rule4Indicator = _rule4Indicators[rnd.Next(0, _rule4Indicators.Length)];
        var rule1EndPlug = (rnd.Next(rule1StartPlug + 1, rule1StartPlug + 3) - 1) % 12 + 1;
        var hasRule4Indicator = rnd.Next(0, 2) != 0 ? Bomb.IsIndicatorOn(rule4Indicator) : Bomb.IsIndicatorOff(rule4Indicator);

        var rule5StartAt = (rnd.Next(0, 12) + 5) % 12;  // Starting wire is at plug this +1, or +2 if the +1 doesn’t have a wire

        var startingLetters = Ex.NewArray(
            Bomb.GetIndicators().SelectMany(s => s).DefaultIfEmpty().Min(),
            Bomb.GetIndicators().SelectMany(s => s).DefaultIfEmpty().Max(),
            snLetters[1],
            snLetters[snLetters.Length - 2],
            snLetters[snLetters.Length - 1],
            snLetters[0]);
        var startingLetter = startingLetters[rnd.Next(0, startingLetters.Length)];
        var fallbackLetter = (char) ('A' + (rnd.Next(0, 26) + 17) % 26);
        if (startingLetter == '\0')
            startingLetter = fallbackLetter;

        var colors = new[] { WireColor.Red, WireColor.Green, WireColor.Blue, WireColor.White, WireColor.Black, WireColor.Yellow };
        rnd.ShuffleFisherYates(colors);
        var useReverseIf = colors.Take(3).ToArray();

        var rules = new List<RuleInfo>();
        if (rnd.Seed == 1)
        {
            rules.Add(((MakeColorRule) _tableRules[35])("A or N", new[] { WireColor.Yellow, WireColor.Blue, WireColor.Green }));
            rules.Add(((MakeSimpleRule) _tableRules[2])("B or O"));
            rules.Add(((MakeSimpleRule) _tableRules[6])("C or P"));
            rules.Add(((MakeColorRule) _tableRules[34])("D or Q", new[] { WireColor.Red, WireColor.Blue, WireColor.Black }));
            rules.Add(((MakeSimpleRule) _tableRules[10])("E or R"));
            rules.Add(((MakeSimpleRule) _tableRules[16])("F or S"));
            rules.Add(((MakeColorRule) _tableRules[34])("G or T", new[] { WireColor.Yellow, WireColor.White, WireColor.Green }));
            rules.Add(((MakeSimpleRule) _tableRules[7])("H or U"));
            rules.Add(((MakeSimpleRule) _tableRules[21])("I or V"));
            rules.Add(((MakeColorRule) _tableRules[35])("J or W", new[] { WireColor.White, WireColor.Black, WireColor.Red }));
            rules.Add(((MakeSimpleRule) _tableRules[9])("K or X"));
            rules.Add(((MakeNumberRule12) _tableRules[29])("L or Y", 6));
            rules.Add(((MakeColorRule) _tableRules[41])("M or Z", new[] { WireColor.White, WireColor.Black }));
        }
        else
        {
            var tableRules = rnd.ShuffleFisherYates(_tableRules.ToArray());
            for (var i = 0; i < 13; i++)
            {
                var name = string.Format("{0} or {1}", (char) ('A' + i), (char) ('N' + i));
                if (tableRules[i] is MakeSimpleRule)
                    rules.Add(((MakeSimpleRule) tableRules[i])(name));
                else if (tableRules[i] is MakeNumberRule12)
                    rules.Add(((MakeNumberRule12) tableRules[i])(name, rnd.Next(2, 12)));
                else if (tableRules[i] is MakeNumberRule6)
                    rules.Add(((MakeNumberRule6) tableRules[i])(name, rnd.Next(9, 12)));
                else if (tableRules[i] is MakeColorRule)
                {
                    rnd.ShuffleFisherYates(colors);
                    rules.Add(((MakeColorRule) tableRules[i])(name, colors.ToArray())); // important to take a copy of the array here so it doesn’t get re-shuffled
                }
            }
        }


        // STEP 3: DETERMINE WHICH WIRES NEED TO BE CUT

        // Figure out the starting wire (as index into wireInfos, rather than peg number)
        int startIndex;
        if (hasRule1Port && (startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == rule1StartPlug && wi.ConnectedTo == rule1EndPlug)) != -1)
        {
            Debug.LogFormat("[Follow the Leader #{0}] Starting at wire {1} because of rule 1.", _moduleId, _wireInfos[startIndex]);
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == rule2Plug)) != -1)
        {
            Debug.LogFormat("[Follow the Leader #{0}] Starting at wire {1} because of rule 2.", _moduleId, _wireInfos[startIndex]);
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == rule3Plug)) != -1)
        {
            Debug.LogFormat("[Follow the Leader #{0}] Starting at wire {1} because of rule 3.", _moduleId, _wireInfos[startIndex]);
        }
        else if (hasRule4Indicator)
        {
            Debug.LogFormat("[Follow the Leader #{0}] Cut everything in reverse order because of rule 4.", _moduleId);
            foreach (var wi in _wireInfos)
                wi.Justification = "Rule 4.";
            _expectedCuts = _wireInfos.OrderByDescending(w => w.ConnectedFrom).ToList();
            goto end;
        }
        else if ((startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == rule5StartAt + 1)) != -1)
        {
            Debug.LogFormat("[Follow the Leader #{0}] Starting at wire {1} because of rule 5.", _moduleId, _wireInfos[startIndex]);
        }
        else
        {
            startIndex = _wireInfos.IndexOf(wi => wi.ConnectedFrom == (rule5StartAt + 1) % 12 + 1);
            Debug.LogFormat("[Follow the Leader #{0}] Starting at wire {1} because of rule 5.", _moduleId, _wireInfos[startIndex]);
        }

        var curStep = (startingLetter - 'A') % rules.Count;
        var reverse = useReverseIf.Contains(_wireInfos[startIndex].WireColor);

        Debug.LogFormat("[Follow the Leader #{0}] Start at rule {1} and go {2}.", _moduleId, startingLetter, reverse ? "backwards" : "forwards");

        _expectedCuts = new List<WireInfo>();
        var wiresSinceStart = new List<WireInfo> { _wireInfos[startIndex] };

        // Finally, determine which wires need cutting
        for (int i = 0; i < _wireInfos.Count; i++)
        {
            var curIndex = (i + startIndex) % _wireInfos.Count;
            var wire = _wireInfos[curIndex];
            if (i == 0)
            {
                // Always cut the first one.
                wire.MustCut = true;
                wire.Justification = "Cut because this is the starting wire.";
            }
            else
            {
                wire.MustCut = rules[curStep].Evaluate(getWire, curIndex, _wireInfos.Count, wiresSinceStart);
                wire.Justification = string.Format("Rule {0}: {1} ⇒ {2}", rules[curStep].Name, rules[curStep].Phrasing, wire.MustCut ? "CUT" : "DON’T CUT");
                curStep = (curStep + (reverse ? -1 : 1) + rules.Count) % rules.Count;
            }
            wiresSinceStart.Add(wire);
            if (wire.MustCut)
                _expectedCuts.Add(wire);
        }

        Debug.LogFormat("[Follow the Leader #{0}] Wire state:\n{1}", _moduleId, string.Join("\n", Enumerable.Range(0, _wireInfos.Count).Select(i => _wireInfos[(i + startIndex) % _wireInfos.Count].ToStringFull()).ToArray()));
        end:
        Debug.LogFormat("[Follow the Leader #{0}] Expectation:\n{1}", _moduleId, string.Join("\n", _expectedCuts.Select(wi => wi.ToString()).ToArray()));
    }

    private WireInfo getWire(int i) { return _wireInfos[(i % _wireInfos.Count + _wireInfos.Count) % _wireInfos.Count]; }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} cut 4 6 9 10 1 2 [cut wires starting at these plugs] | !{0} colorblind";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        if (command == "colorblind")
        {
            for (int i = 0; i < _wireInfos.Count; i++)
                _wireInfos[i].EnableColorBlindMode();
            return new KMSelectable[0];
        }
        if (!command.StartsWith("cut ", StringComparison.OrdinalIgnoreCase))
            return null;
        command = command.Substring(4);
        var wires = new List<KMSelectable>();
        foreach (var cmd in command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int number;
            if (!int.TryParse(cmd, out number))
                return null;
            var wireInfo = _wireInfos.FirstOrDefault(wi => wi.ConnectedFrom == number);
            if (wireInfo == null)
                return null;
            wires.Add(wireInfo.Selectable);
        }
        return wires.ToArray();
    }
}

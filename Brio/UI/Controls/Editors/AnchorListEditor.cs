using Brio.Capabilities.Posing;
using Brio.Game.Posing;
using Brio.Game.Posing.Skeletons;
using Brio.UI.Controls.Stateless;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Brio.UI.Controls.Editors;

public static class AnchorListEditor
{
    private const float FieldWidth = 220f;

    // Switching bones implicitly resets this because we re-resolve by id against the
    // current bone's anchor list each frame; if not found, we fall back to the first.
    private static Guid _activeAnchorId = Guid.Empty;

    private static readonly Regex _trailingNumberRegex = new(@"#(\d+)$", RegexOptions.Compiled);

    public static void Draw(string id, PosingCapability posing)
    {
        if(!Brio.TryGetService<AnchorService>(out var anchorService))
            return;

        using(ImRaii.PushId(id))
        {
            var selectedBoneId = posing.IsSelectedBone();
            Bone? selectedBone = selectedBoneId.HasValue ? posing.SkeletonPosing.GetBone(selectedBoneId.Value) : null;

            if(selectedBone is null || !selectedBoneId.HasValue)
            {
                ImGui.TextDisabled("Select a bone to manage its anchors.");
                return;
            }

            var matching = CollectAnchors(anchorService, posing, selectedBone, selectedBoneId.Value);

            // The header can mutate `matching` (create/delete), so anything below it must
            // operate on the post-header list, not a stale snapshot.
            DrawHeader(anchorService, posing, selectedBone, selectedBoneId.Value, matching);

            if(matching.Count == 0)
                return;

            int activeIndex = matching.FindIndex(a => a.Id == _activeAnchorId);
            if(activeIndex < 0)
            {
                activeIndex = 0;
                _activeAnchorId = matching[0].Id;
            }

            DrawAnchorDropdown(matching, ref activeIndex);

            ImGui.Separator();

            var active = matching[activeIndex];
            using(ImRaii.PushId(active.Id.ToString()))
            {
                DrawAnchorEditor(active, anchorService);
            }
        }
    }

    private static List<PoseAnchor> CollectAnchors(AnchorService anchorService, PosingCapability posing, Bone selectedBone, BonePoseInfoId selectedBoneId)
    {
        var matching = new List<PoseAnchor>();
        foreach(var anchor in anchorService.Anchors)
        {
            if(anchor.SourceActorId.Equals(posing.Actor.Id)
                && anchor.SourceSlot == selectedBoneId.Slot
                && anchor.SourceBoneName == selectedBone.Name
                && anchor.SourceBonePartial == selectedBone.PartialId)
            {
                matching.Add(anchor);
            }
        }
        return matching;
    }

    private static void DrawHeader(AnchorService anchorService, PosingCapability posing, Bone selectedBone, BonePoseInfoId selectedBoneId, List<PoseAnchor> matching)
    {
        if(ImBrio.FontIconButton("create_anchor", FontAwesomeIcon.Plus, "Add a new anchor on this bone"))
        {
            string displayName = BuildNextAnchorName(matching, posing.Actor.FriendlyName, selectedBone.FriendlyName);

            var created = anchorService.Create(
                posing.Actor.Id,
                selectedBoneId.Slot,
                selectedBone.Name,
                selectedBone.PartialId,
                displayName);

            // Keep `matching` in sync so the auto-select below resolves on the same frame.
            matching.Add(created);
            _activeAnchorId = created.Id;
        }

        ImGui.SameLine();

        // Ctrl-gated delete — placed next to + so destructive controls live together.
        bool ctrl = ImGui.GetIO().KeyCtrl;
        bool canDelete = matching.Count > 0 && ctrl;
        using(ImRaii.Disabled(!canDelete))
        {
            string deleteTooltip = matching.Count == 0
                ? "No anchor to delete"
                : ctrl
                    ? "Delete current anchor"
                    : "Hold Ctrl to delete the current anchor";

            if(ImBrio.FontIconButton("delete_anchor", FontAwesomeIcon.Trash, deleteTooltip))
            {
                int idx = matching.FindIndex(a => a.Id == _activeAnchorId);
                if(idx < 0) idx = 0;
                var toRemove = matching[idx];

                anchorService.Remove(toRemove.Id);
                matching.RemoveAt(idx);

                _activeAnchorId = matching.Count > 0 ? matching[Math.Min(idx, matching.Count - 1)].Id : Guid.Empty;
            }
        }

        ImGui.SameLine();
        ImGui.Text(matching.Count == 0
            ? "No anchors on this bone yet."
            : $"{matching.Count} anchor{(matching.Count > 1 ? "s" : "")} on this bone");
    }

    private static void DrawAnchorDropdown(List<PoseAnchor> matching, ref int activeIndex)
    {
        // Display the raw name only — the #N is already part of the name itself,
        // so prefixing here would produce things like "#1 Foo #2" after deletions.
        string preview = matching[activeIndex].DisplayName;
        ImGui.SetNextItemWidth(FieldWidth);
        using var combo = ImRaii.Combo("Anchor", preview);
        if(!combo.Success)
            return;

        for(int i = 0; i < matching.Count; i++)
        {
            bool isSelected = i == activeIndex;
            string label = $"{matching[i].DisplayName}###anchor_choice_{matching[i].Id}";
            if(ImGui.Selectable(label, isSelected))
            {
                activeIndex = i;
                _activeAnchorId = matching[i].Id;
            }
        }
    }

    private static string BuildNextAnchorName(List<PoseAnchor> existing, string actorName, string boneName)
    {
        // Find the highest #N already used on this bone, so deletions don't reuse numbers.
        int next = 1;
        foreach(var a in existing)
        {
            var match = _trailingNumberRegex.Match(a.DisplayName);
            if(match.Success && int.TryParse(match.Groups[1].Value, out var n) && n >= next)
                next = n + 1;
        }
        return $"{actorName} / {boneName} #{next}";
    }

    private static void DrawAnchorEditor(PoseAnchor anchor, AnchorService anchorService)
    {
        string name = anchor.DisplayName;
        ImGui.SetNextItemWidth(FieldWidth);
        if(ImGui.InputText("Name###anchor_name", ref name, 128))
            anchor.DisplayName = name;

        if(!anchorService.TryGetSourceBone(anchor, out _))
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "(source bone unresolved)");

        int frame = (int)anchor.OffsetFrame;
        ImGui.SetNextItemWidth(FieldWidth);
        if(ImGui.Combo("Frame###anchor_frame", ref frame, "Source local\0World\0\0"))
            anchor.OffsetFrame = (FollowOffsetFrame)frame;

        ImGui.SetNextItemWidth(FieldWidth);
        ImGui.DragFloat3("Pos###anchor_pos", ref anchor.PositionOffset, 0.005f);

        ImGui.SetNextItemWidth(FieldWidth);
        ImGui.DragFloat3("Rot (deg)###anchor_rot", ref anchor.RotationOffsetEuler, 0.5f);

        ImGui.SetNextItemWidth(FieldWidth);
        ImGui.DragFloat3("Scale###anchor_scl", ref anchor.ScaleOffset, 0.01f);
    }
}

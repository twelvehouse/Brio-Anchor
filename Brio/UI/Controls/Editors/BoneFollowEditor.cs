using Brio.Capabilities.Posing;
using Brio.Game.Posing;
using Brio.UI.Controls.Stateless;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System;

namespace Brio.UI.Controls.Editors;

public static class BoneFollowEditor
{
    public static void Draw(string id, BonePoseInfo poseInfo, PosingCapability posing)
    {
        if(!Brio.TryGetService<AnchorService>(out var anchorService))
            return;

        var follow = poseInfo.Follow ??= new BoneFollowInfo();
        bool changed = false;

        using(ImRaii.PushId(id))
        {
            changed |= ImGui.Checkbox("Enabled", ref follow.Enabled);

            ImGui.SameLine();
            if(ImBrio.FontIconButton("clear_follow", FontAwesomeIcon.Trash, "Clear follow settings"))
            {
                poseInfo.Follow = null;
                return;
            }

            ImGui.Separator();

            using(ImRaii.Disabled(!follow.Enabled))
            {
                changed |= DrawAnchorCombo(follow, anchorService);

                if(anchorService.Anchors.Count == 0)
                    ImGui.TextDisabled("No anchors yet. Create one from the Anchors popup.");

                ImGui.Separator();

                changed |= ImGui.Checkbox("Position###follow_pos", ref follow.FollowPosition);
                ImGui.SameLine();
                changed |= ImGui.Checkbox("Rotation###follow_rot", ref follow.FollowRotation);
                ImGui.SameLine();
                changed |= ImGui.Checkbox("Scale###follow_scl", ref follow.FollowScale);

                ImGui.Separator();

                ImGui.TextDisabled(poseInfo.DefaultIK.Enabled
                    ? "IK is ON — the follow position drives the IK solver."
                    : "IK is OFF — the follow position is applied directly. Toggle IK in the IK popup.");
            }
        }

        if(changed)
            poseInfo.Follow = follow;
    }

    private static bool DrawAnchorCombo(BoneFollowInfo follow, AnchorService anchorService)
    {
        var current = anchorService.Get(follow.AnchorId);
        string preview = current?.DisplayName ?? "(none)";
        bool changed = false;

        using var combo = ImRaii.Combo("Anchor", preview);
        if(combo.Success)
        {
            if(ImGui.Selectable("(none)", follow.AnchorId == Guid.Empty))
            {
                follow.AnchorId = Guid.Empty;
                changed = true;
            }

            foreach(var anchor in anchorService.Anchors)
            {
                bool isSelected = anchor.Id == follow.AnchorId;
                if(ImGui.Selectable($"{anchor.DisplayName}###follow_anchor_{anchor.Id}", isSelected))
                {
                    follow.AnchorId = anchor.Id;
                    changed = true;
                }
            }
        }
        return changed;
    }
}

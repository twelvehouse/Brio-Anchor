using Brio.Entities.Core;
using System;
using System.Numerics;

namespace Brio.Game.Posing;

// A user-managed reference point bound to a source bone, with editable offsets.
// Other bones reference an anchor by id to follow it (see BoneFollowInfo).
public class PoseAnchor
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public EntityId SourceActorId;
    public PoseInfoSlot SourceSlot = PoseInfoSlot.Character;
    public string SourceBoneName = string.Empty;
    public int SourceBonePartial = 0;

    public Vector3 PositionOffset = Vector3.Zero;
    public Vector3 RotationOffsetEuler = Vector3.Zero;
    public Vector3 ScaleOffset = Vector3.Zero;

    public FollowOffsetFrame OffsetFrame = FollowOffsetFrame.SourceLocal;
}

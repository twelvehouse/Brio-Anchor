using Brio.Capabilities.Posing;
using Brio.Core;
using Brio.Entities;
using Brio.Entities.Core;
using Brio.Game.Actor.Interop;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Brio.Game.Posing;

public class AnchorService
{
    private readonly EntityManager _entityManager;
    private readonly List<PoseAnchor> _anchors = new();

    public IReadOnlyList<PoseAnchor> Anchors => _anchors;

    public AnchorService(EntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public PoseAnchor Create(EntityId actorId, PoseInfoSlot slot, string boneName, int partial, string displayName)
    {
        var anchor = new PoseAnchor
        {
            DisplayName = displayName,
            SourceActorId = actorId,
            SourceSlot = slot,
            SourceBoneName = boneName,
            SourceBonePartial = partial,
        };
        _anchors.Add(anchor);
        return anchor;
    }

    public void Remove(Guid id)
    {
        _anchors.RemoveAll(a => a.Id == id);
    }

    public PoseAnchor? Get(Guid id)
    {
        if(id == Guid.Empty)
            return null;

        foreach(var anchor in _anchors)
        {
            if(anchor.Id == id)
                return anchor;
        }
        return null;
    }

    // Computes the anchor's world-space transform: source bone world + the anchor's offset.
    // Returns false if the source actor/bone is no longer resolvable this frame.
    public unsafe bool TryGetWorldTransform(PoseAnchor anchor, out Transform world)
    {
        world = Transform.Identity;

        if(!_entityManager.TryGetEntity(anchor.SourceActorId, out var entity))
            return false;

        if(!entity.TryGetCapability<SkeletonPosingCapability>(out var sp))
            return false;

        var bone = sp.GetBone(new BonePoseInfoId(anchor.SourceBoneName, anchor.SourceBonePartial, anchor.SourceSlot));
        if(bone is null || bone.Skeleton.CharacterBase is null)
            return false;

        var actorWorld = GetActorWorldTransform(bone.Skeleton.CharacterBase);
        var srcModel = bone.LastTransform;

        var srcWorldPos = actorWorld.Position + Vector3.Transform(srcModel.Position * actorWorld.Scale, actorWorld.Rotation);
        var srcWorldRot = actorWorld.Rotation * srcModel.Rotation;

        var rotOffset = EulerDegToQuat(anchor.RotationOffsetEuler);

        Vector3 anchorWorldPos;
        Quaternion anchorWorldRot;
        if(anchor.OffsetFrame == FollowOffsetFrame.SourceLocal)
        {
            anchorWorldPos = srcWorldPos + Vector3.Transform(anchor.PositionOffset, srcWorldRot);
            anchorWorldRot = srcWorldRot * rotOffset;
        }
        else
        {
            anchorWorldPos = srcWorldPos + anchor.PositionOffset;
            anchorWorldRot = rotOffset * srcWorldRot;
        }

        world = new Transform
        {
            Position = anchorWorldPos,
            Rotation = anchorWorldRot,
            Scale = srcModel.Scale + anchor.ScaleOffset
        };
        return true;
    }

    public bool TryGetSourceBone(PoseAnchor anchor, out Game.Posing.Skeletons.Bone? bone)
    {
        bone = null;
        if(!_entityManager.TryGetEntity(anchor.SourceActorId, out var entity))
            return false;
        if(!entity.TryGetCapability<SkeletonPosingCapability>(out var sp))
            return false;

        bone = sp.GetBone(new BonePoseInfoId(anchor.SourceBoneName, anchor.SourceBonePartial, anchor.SourceSlot));
        return bone != null;
    }

    private static unsafe Transform GetActorWorldTransform(BrioCharacterBase* charaBase)
    {
        return new Transform
        {
            Position = charaBase->CharacterBase.DrawObject.Object.Position,
            Rotation = charaBase->CharacterBase.DrawObject.Object.Rotation,
            Scale = (Vector3)charaBase->CharacterBase.DrawObject.Object.Scale * charaBase->ScaleFactor
        };
    }

    private static Quaternion EulerDegToQuat(Vector3 eulerDeg)
    {
        const float toRad = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(eulerDeg.Y * toRad, eulerDeg.X * toRad, eulerDeg.Z * toRad);
    }
}

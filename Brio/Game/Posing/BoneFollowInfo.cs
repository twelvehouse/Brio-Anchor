using System;

namespace Brio.Game.Posing;

public enum FollowOffsetFrame
{
    SourceLocal,
    World,
}

public class BoneFollowInfo
{
    public bool Enabled = false;

    public Guid AnchorId = Guid.Empty;

    public bool FollowPosition = true;
    public bool FollowRotation = false;
    public bool FollowScale = false;

    public bool HasValidAnchor => AnchorId != Guid.Empty;
}

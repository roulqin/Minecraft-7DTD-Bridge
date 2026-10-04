namespace MC7DTD
{
    public sealed class AvatarProxyScene : IRotatingProxyScene
    {
        sealed class Entry { public AvatarRenderer.Handle Avatar; public double X,Y,Z,Yaw,Pitch; public bool InitialRotation=true; }
        readonly AvatarRenderer renderer;
        public AvatarProxyScene(AvatarRenderer renderer) { this.renderer = renderer; }
        public object Create(string name, double x, double y, double z)
        { return new Entry { Avatar = renderer.CreateAvatar(name,x,y,z), X=x, Y=y, Z=z }; }
        public void Move(object value, double x, double y, double z)
        { var e=(Entry)value; e.X=x;e.Y=y;e.Z=z;renderer.UpdateAvatar(e.Avatar,x,y,z,e.Yaw,e.Pitch); }
        public void Rotate(object value, double yaw, double pitch, double roll)
        {
            var e=(Entry)value;e.Yaw=yaw;e.Pitch=pitch;renderer.UpdateAvatar(e.Avatar,e.X,e.Y,e.Z,yaw,pitch);
            if(e.InitialRotation){
                e.InitialRotation=false;
                if(e.Avatar.Fallback==null&&e.Avatar.MotionSettings.Enabled){e.Avatar.Motion.SnapInitialPose();e.Avatar.Pose.RenderFrame();e.Avatar.Pose.Apply();}
            }
        }
        public void Delete(object value) { renderer.RemoveAvatar(((Entry)value).Avatar); }
    }
}

using System;
using UnityEngine;
namespace MC7DTD
{
    public sealed class AvatarEquipmentAnimation:IDisposable
    {
        readonly AnimatorOverrideController controller;
        readonly AnimationClip walk,run,heldWalk,heldRun;
        bool holding;
        public AvatarEquipmentAnimation(Animator animator,AnimationClip heldWalk,AnimationClip heldRun)
        {
            this.heldWalk=heldWalk;this.heldRun=heldRun;controller=new AnimatorOverrideController(animator.runtimeAnimatorController);
            walk=controller["Walk"];run=controller["Run"];animator.runtimeAnimatorController=controller;
        }
        public void SetHolding(bool value){if(holding==value)return;holding=value;controller["Walk"]=value?heldWalk:walk;controller["Run"]=value?heldRun:run;}
        public void Dispose(){UnityEngine.Object.Destroy(controller);}
    }
}

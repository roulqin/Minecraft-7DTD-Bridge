using System;
using System.Linq;
using UnityEngine;

namespace MC7DTD
{
    // Only Animator parameters. Never writes a bone, transform fact, or network field.
    public sealed class AvatarAnimatorDriver
    {
        bool wasGrounded = true;
        public int JumpStarts { get; private set; }
        public void Update(Animator animator, AvatarActionState action, AvatarAnimationState velocity, bool localAction)
        {
            bool refined = animator.parameters.Any(p => p.name == "jumpStart");
            bool grounded = !localAction || action.Grounded;
            double vertical = localAction ? action.VerticalVelocity : 0;
            double speed = localAction ? action.Speed : velocity.Speed;
            animator.SetFloat("speed", refined ? (float)Math.Min(40, speed) : velocity.AnimatorSpeed);
            animator.speed = grounded ? velocity.PlaybackSpeed : 1;
            if (animator.parameters.Any(p => p.name == "actionState"))
            {
                animator.SetBool("isGrounded", grounded);
                animator.SetFloat("verticalVelocity", (float)vertical);
                animator.SetInteger("actionState", localAction ? action.Code : 0);
            }
            if (refined && wasGrounded && !grounded && vertical > .15)
            { animator.SetTrigger("jumpStart"); JumpStarts++; }
            if (refined && grounded) animator.ResetTrigger("jumpStart");
            wasGrounded = grounded;
        }
        public void Reset() { wasGrounded = true; JumpStarts = 0; }
        public static string State(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled) return "none";
            var s = animator.GetCurrentAnimatorStateInfo(0);
            foreach (var name in new[] { "JumpStart", "JumpLoop", "Fall", "Land" })
                if (s.IsName(name)) return name == "JumpStart" ? "jump_start" : name == "JumpLoop" ? "jump_loop" : name.ToLowerInvariant();
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length == 0) return "unknown";
            return clips.OrderByDescending(c => c.weight).First().clip.name.ToLowerInvariant();
        }
        public static double Blend(Animator animator)
        {
            if (animator == null) return 0;
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            return clips.Where(c => c.clip.name == "Run").Sum(c => (double)c.weight);
        }
    }
}

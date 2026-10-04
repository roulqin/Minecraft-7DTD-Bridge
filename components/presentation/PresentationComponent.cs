using System;
using System.Runtime.Serialization;
using System.Globalization;

namespace MC7DTD
{
    [DataContract]
    public sealed class PresentationComponent
    {
        [DataMember(Name="renderer", EmitDefaultValue=false)] public string Renderer;
        [DataMember(Name="model", EmitDefaultValue=false)] public string Model;
        [DataMember(Name="variant", EmitDefaultValue=false)] public string Variant;
        [DataMember(Name="scale", EmitDefaultValue=false)] public double? Scale;
        public PresentationComponent Copy() => new PresentationComponent { Renderer=Renderer,Model=Model,Variant=Variant,Scale=Scale };
        public static PresentationComponent Default() => new PresentationComponent { Renderer="unknown",Model="default",Scale=1 };
        public PresentationComponent Merge(PresentationComponent patch) => new PresentationComponent {
            Renderer=patch.Renderer ?? Renderer, Model=patch.Model ?? Model, Variant=patch.Variant ?? Variant, Scale=patch.Scale ?? Scale };
        public override string ToString() => "renderer="+Renderer+" model="+Model+" variant="+(Variant ?? "none")+" scale="+Scale?.ToString("R",CultureInfo.InvariantCulture);
    }
    [DataContract]
    public sealed class PresentationComponents
    {
        // Null is an explicit remove; absent outer 'components' means no change.
        [DataMember(Name="presentation")] public PresentationComponent Presentation;
    }
}

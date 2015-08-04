using Orchard.UI.Resources;

namespace Orchard.jQuery.Ias {
    public class ResourceManifest : IResourceManifestProvider {
        public void BuildManifests(ResourceManifestBuilder builder) {
            var manifest = builder.Add();
            manifest.DefineScript("jQuery_Ias").SetUrl("jquery.ias.min.js", "jquery.ias.min.js").SetVersion("0.1.6").SetDependencies("jQuery");
            manifest.DefineStyle("jQuery_Ias").SetUrl("jquery.ias.css").SetVersion("0.1.6");
        }
    }
}

using MarkdownGallery.Wasm.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The gallery has one page, so it is the root component and no router is needed.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<GalleryPage>("#app");
await builder.Build().RunAsync();

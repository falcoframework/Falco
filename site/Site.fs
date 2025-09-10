open System
open System.IO
open System.Text.RegularExpressions
open Falco.Markup
open Markdig
open Markdig.Syntax.Inlines
open Markdig.Renderers
open Markdig.Syntax

type LayoutModel =
    { Title : string
      MainContent : string }

type LayoutTwoColModel =
    { Title : string
      SideContent : XmlNode list
      MainContent : string }

type ParsedMarkdownDocument =
    { Title : string
      Body  : string }
module Markdown =
    let render (markdown : string) : ParsedMarkdownDocument =
        // Render Markdown as HTML
        let pipeline =
            MarkdownPipelineBuilder()
                .UseAutoIdentifiers()
                .UsePipeTables()
                .UseAutoLinks()
                .Build()

        use sw = new StringWriter()
        let renderer = HtmlRenderer(sw)

        pipeline.Setup(renderer) |> ignore

        let doc = Markdown.Parse(markdown, pipeline)

        renderer.Render(doc) |> ignore
        sw.Flush() |> ignore
        let renderedMarkdown = sw.ToString()

        // Extract title
        let title =
            doc.Descendants<HeadingBlock>()
            |> Seq.tryFind (fun x -> x.Level = 1)
            |> Option.bind (fun x ->
                x.Inline
                |> Seq.tryPick (fun i ->
                    match i with
                    | :? LiteralInline as literal -> Some(literal.ToString())
                    | _ -> None))


        // Rewrite direct markdown doc links
        let body = Regex.Replace(renderedMarkdown.Replace("\"documentation/", "\"docs/"), "([a-zA-Z\-]+)\.md", "$1.html")

        { Title = title |> Option.defaultValue ""
          Body = body }

    let renderFile (path : string) =
        render (File.ReadAllText(path))

module View =
    let docsLinks =
        [
            Elem.h3 [] [ Text.raw "Project Links" ]
            Elem.a [ Attr.href "/"] [ Text.raw "Project Homepage" ]
            Elem.a [ Attr.class' "db"; Attr.href "https://github.com/FalcoFramework/Falco"; Attr.targetBlank ]
                [ Text.raw "Source Code" ]
            Elem.a [ Attr.class' "db"; Attr.href "https://github.com/FalcoFramework/Falco/issues"; Attr.targetBlank ]
                [ Text.raw "Issue Tracker" ]
            Elem.a [ Attr.class' "db"; Attr.href "https://github.com/FalcoFramework/Falco/discussions"; Attr.targetBlank ]
                [ Text.raw "Discussion" ]
            Elem.a [ Attr.class' "db"; Attr.href "https://twitter.com/falco_framework"; Attr.targetBlank ]
                [ Text.raw "Twitter" ]
        ]

    let docsNav =
        [
            Text.h3 "Contents"
            Elem.ul [ Attr.class' "nl3 f6" ] [
                Elem.li [] [ Elem.a [ Attr.href "get-started.html" ] [ Text.raw "Getting Started" ] ]
                Elem.li [] [ Elem.a [ Attr.href "routing.html" ] [ Text.raw "Routing" ] ]
                Elem.li [] [ Elem.a [ Attr.href "response.html" ] [ Text.raw "Response Writing" ] ]
                Elem.li [] [ Elem.a [ Attr.href "request.html" ] [ Text.raw "Request Handling" ] ]
                Elem.li [] [ Elem.a [ Attr.href "markup.html" ] [ Text.raw "Markup" ] ]
                Elem.li [] [
                    Text.raw "Security"
                    Elem.ul [] [
                        Elem.li [] [ Elem.a [ Attr.href "cross-site-request-forgery.html" ] [ Text.raw "Cross Site Request Forgery (XSRF)" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "authentication.html" ] [ Text.raw "Authentication & Authorization" ] ]
                    ]
                ]
                Elem.li [] [ Elem.a [ Attr.href "host-configuration.html" ] [ Text.raw "Host Configuration" ] ]
                Elem.li [] [ Elem.a [ Attr.href "deployment.html" ] [ Text.raw "Deployment" ] ]
                Elem.li [] [
                    Text.raw "Examples"
                    Elem.ul [] [
                        Elem.li [] [ Elem.a [ Attr.href "example-hello-world.html" ] [ Text.raw "Hello World" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-hello-world-mvc.html" ] [ Text.raw "Hello World MVC" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-dependency-injection.html" ] [ Text.raw "Dependency Injection" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-external-view-engine.html" ] [ Text.raw "External View Engine" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-basic-rest-api.html" ] [ Text.raw "Basic REST API" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-open-api.html" ] [ Text.raw "Open API" ] ]
                        Elem.li [] [ Elem.a [ Attr.href "example-htmx.html" ] [ Text.raw "htmx" ] ]
                    ]
                ]
                Elem.li [] [ Elem.a [ Attr.href "migrating-from-v4-to-v5.html" ] [ Text.raw "V5 Migration Guide" ] ]
            ]
        ]

    let private _layoutHead title =
        let title =
            if String.IsNullOrWhiteSpace(title) then
                "Falco - F# web toolkit for ASP.NET Core"
            else
                $"{title} - Falco Documentation"

        [
            Elem.meta  [ Attr.charset "UTF-8" ]
            Elem.meta  [ Attr.httpEquiv "X-UA-Compatible"; Attr.content "IE=edge, chrome=1" ]
            Elem.meta  [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            Elem.title [] [ Text.raw title ]
            Elem.meta  [ Attr.name "description"; Attr.content "A functional-first toolkit for building brilliant ASP.NET Core applications using F#." ]

            Elem.link [ Attr.rel "shortcut icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]
            Elem.link [ Attr.rel "icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]
            Elem.link [ Attr.rel "preconnect"; Attr.href "https://fonts.gstatic.com" ]
            Elem.link [ Attr.href "https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;700&display=swap"; Attr.rel "stylesheet" ]
            Elem.link [ Attr.href "/prism.css"; Attr.rel "stylesheet" ]
            Elem.link [ Attr.href "/tachyons.css"; Attr.rel "stylesheet" ]
            Elem.link [ Attr.href "/style.css"; Attr.rel "stylesheet" ]

            Elem.script [ Attr.async; Attr.src "https://www.googletagmanager.com/gtag/js?id=G-D62HSJHMNZ" ] []
            Elem.script [] [ Text.raw """window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', 'G-D62HSJHMNZ');"""
            ]
        ]

    let private _layoutFooter =
        Elem.footer [ Attr.class' "cl pa3 bg-merlot" ] [
            Elem.div [ Attr.class' "f7 tc white-70" ]
                [ Text.raw $"&copy; 2020-{DateTime.Now.Year} Pim Brouwers & contributors." ]
        ]

    let layout (model : LayoutModel) =
        let topBar =
            Elem.div [] [
                Elem.nav [ Attr.class' "flex flex-column flex-row-l items-center" ] [
                    Elem.a [ Attr.href "/" ]
                        [ Elem.img [ Attr.src "/icon.svg"; Attr.class' "w3 pb3 pb0-l o-80 hover-o-100" ] ]
                    Elem.div [ Attr.class' "flex-grow-1-l tc tr-l" ] [
                        Elem.a [ Attr.href "/docs"; Attr.title "Overview of Falco's key features"; Attr.class' "dib mh2 mh3-l no-underline white-90 hover-white" ]
                            [ Text.raw "docs" ]
                        Elem.a [ Attr.href "https://github.com/FalcoFramework/Falco"; Attr.title "Fork Falco on GitHub"; Attr.alt "Falco GitHub Link"; Attr.targetBlank; Attr.class' "dib mh2 ml3-l no-underline white-90 hover-white" ]
                            [ Text.raw "code" ]
                        Elem.a [ Attr.href "https://github.com/FalcoFramework/Falco/tree/master/examples"; Attr.title "Falco code samples"; Attr.alt "Faclo code samples link"; Attr.class' "dib ml2 mh3-l no-underline white-90 hover-white" ]
                            [ Text.raw "samples" ]
                        Elem.a [ Attr.href "https://github.com/FalcoFramework/Falco/discussions"; Attr.title "Need help?"; Attr.alt "Faclo GitHub discussions link"; Attr.class' "dib ml2 mh3-l no-underline white-90 hover-white" ]
                            [ Text.raw "help" ]
                    ]
                ]
            ]

        let greeting =
            Elem.div [ Attr.class' "mw6 center pb5 noto tc fw4 lh-copy white" ] [
                Elem.h1 [ Attr.class' "mt4 mb3 fw4 f2" ]
                    [ Text.raw "Meet Falco." ]
                Elem.h2 [ Attr.class' "mt0 mb4 fw4 f4 f3-l" ]
                    [ Text.raw "Falco is a toolkit for building fast and functional-first web applications using F#." ]

                Elem.div [ Attr.class' "tc" ] [
                    Elem.a [ Attr.href "/docs/get-started.html"; Attr.title "Learn how to get started using Falco"; Attr.class' "dib mh2 mb2 ph3 pv2 merlot bg-white ba b--white br2 no-underline" ]
                        [ Text.raw "Get Started" ]
                    Elem.a [ Attr.href "#falco"; Attr.class' "dib mh2 ph3 pv2 white ba b--white br2 no-underline" ]
                        [ Text.raw "Learn More" ]
                ]
            ]

        let releaseInfo =
            Elem.div [ Attr.class' "mb4 bt b--white-20 tc lh-solid" ] [
                Elem.a [ Attr.href "https://www.nuget.org/packages/Falco"; Attr.class' "dib center ph1 ph4-l pv3 bg-merlot white no-underline ty--50"; Attr.targetBlank ]
                    [ Text.raw "Latest release: 5.1.0 (September, 9, 2025)" ]
            ]

        let benefits =
            Elem.div [ Attr.class' "cf tc lh-copy" ] [
                Elem.div [ Attr.class' "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    Elem.img [ Attr.src "/icons/fast.svg"; Attr.class' "w4 o-90" ]
                    Elem.h3 [ Attr.class' "mv2 white" ]
                        [ Text.raw "Blazing Fast" ]
                    Elem.div [ Attr.class' "mb3 white-90" ]
                        [ Text.raw "Built upon core ASP.NET components." ]
                    Elem.a [ Attr.href "https://web-frameworks-benchmark.netlify.app/result?l=fsharp"; Attr.targetBlank; Attr.class' "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ Text.raw "Learn More" ]
                 ]

                Elem.div [ Attr.class' "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    Elem.img [ Attr.src "/icons/easy.svg"; Attr.class' "w4 o-90" ]
                    Elem.h3 [ Attr.class' "mv2 white" ] [ Text.raw "Easy to Learn" ]
                    Elem.div [ Attr.class' "mb3 white-90" ] [ Text.raw "Designed for getting up to speed quickly." ]
                    Elem.a [ Attr.href "/docs/get-started.html"; Attr.title "Learn how to get started using Falco"; Attr.class' "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ Text.raw "Get Started" ]
                 ]

                Elem.div [ Attr.class' "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    Elem.img [ Attr.src "/icons/view.svg"; Attr.class' "w4 o-90" ]
                    Elem.h3 [ Attr.class' "mv2 white" ] [ Text.raw "Native View Engine" ]
                    Elem.div [ Attr.class' "mb3 white-90" ] [ Text.raw "Markup is written in F# and compiled." ]
                    Elem.a [ Attr.href "/docs/markup.html"; Attr.title "View examples of Falco markup module"; Attr.class' "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ Text.raw "See Examples" ]
                 ]

                Elem.div [ Attr.class' "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l" ] [
                    Elem.img [ Attr.src "/icons/integrate.svg"; Attr.class' "w4 o-90" ]
                    Elem.h3 [ Attr.class' "mv2 white" ] [ Text.raw "Extensible" ]
                    Elem.div [ Attr.class' "mb3 white-90" ] [ Text.raw "Seamlessly integrates with existing libraries." ]
                    Elem.a [ Attr.href "https://github.com/FalcoFramework/Falco/tree/master/samples/ScribanExample"; Attr.targetBlank; Attr.title "Example of incorporating a third-party view engine"; Attr.class' "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ Text.raw "Explore How" ]
                 ]
            ]

        Elem.html [ Attr.lang "en"; ] [
            Elem.head [] (_layoutHead model.Title)
            Elem.body [ Attr.class' "noto bg-merlot bg-dots bg-parallax" ] [
                Elem.header [ Attr.class' "pv3" ] [
                    Elem.div [ Attr.class' "mw8 center pa3" ] [
                        topBar
                        greeting
                        releaseInfo
                        benefits
                    ]
                ]

                Elem.div [ Attr.class' "h100vh bg-white" ] [
                    Elem.div [ Attr.class' "cf mw8 center pv4 ph3" ] [
                        Elem.main [] [ Text.raw model.MainContent ]
                    ]
                ]

                _layoutFooter

                Elem.script [ Attr.src "/prism.js" ] []
            ]
        ]

    let layoutTwoCol (model : LayoutTwoColModel) =
        Elem.html [ Attr.lang "en"; ] [
            Elem.head [] (_layoutHead model.Title)
            Elem.body [ Attr.class' "noto lh-copy" ] [
                Elem.div [ Attr.class' "min-vh-100 mw9 center pa3 overflow-hidden" ] [
                    Elem.nav [ Attr.class' "sidebar w-20-l fl-l mb3 mb0-l" ] [
                        Elem.div [ Attr.class' "flex items-center" ] [
                            Elem.a [ Attr.href "/docs"; Attr.class' "db w3 w4-l" ]
                                [ Elem.img [ Attr.src "/brand.svg"; Attr.class' "br3" ] ]
                            Elem.h2 [ Attr.class' "dn-l mt3 ml3 fw4 gray" ]
                                [ Text.raw "Falco Documentation" ]
                        ]
                        Elem.div [ Attr.class' "dn db-l" ] model.SideContent
                    ]
                    Elem.main [ Attr.class' "w-80-l fl-l pl3-l" ] [ Text.raw model.MainContent ]
                ]
                _layoutFooter
                Elem.script [ Attr.src "/prism.js" ] []
            ]
        ]

module Docs =
    let build (docs : FileInfo[]) (buildDir : DirectoryInfo) =
        if not(buildDir.Exists) then buildDir.Create()

        for file in docs do
            let buildFilename, sideContent =
                if file.Name = "readme.md" then
                    "index.html", View.docsLinks
                else
                    Path.ChangeExtension(file.Name, ".html"), View.docsNav

            let parsedMarkdownDocument = Markdown.renderFile file.FullName

            let html =
                { Title = parsedMarkdownDocument.Title
                  SideContent = sideContent
                  MainContent = parsedMarkdownDocument.Body }
                |> View.layoutTwoCol
                |> renderHtml

            File.WriteAllText(Path.Join(buildDir.FullName, buildFilename), html)

[<EntryPoint>]
let main args =
    if args.Length = 0 then
        failwith "Must provide the working directory as the first argument"

    let workingDir = DirectoryInfo(if args.Length = 2 then args[1] else args[0])

    // Clean build
    printfn "Clearing build directory..."
    let buildDirPath = DirectoryInfo(Path.Join(workingDir.FullName, "../docs"))

    if buildDirPath.Exists then
        for file in buildDirPath.EnumerateFiles("*.html", EnumerationOptions(RecurseSubdirectories = true)) do
            file.Delete()
    else
        buildDirPath.Create ()

    printfn "Rendering homepage..."
    let indexMarkdown = Path.Join(workingDir.FullName, "../README.md") |> File.ReadAllText
    let mainContent = Markdown.render indexMarkdown

    { Title = String.Empty
      MainContent = mainContent.Body }
    |> View.layout
    |> renderHtml
    |> fun text -> File.WriteAllText(Path.Join(buildDirPath.FullName, "index.html"), text)

    printfn "Rendering docs..."
    let docsDir = DirectoryInfo(Path.Join(workingDir.FullName, "../documentation"))
    let docsBuildDir = DirectoryInfo(Path.Join(buildDirPath.FullName, "docs"))
    let readme = FileInfo(Path.Join(workingDir.FullName, "../readme.md"))
    let docFiles = Array.append [|readme|] (docsDir.GetFiles("*.md"))
    Docs.build docFiles docsBuildDir

    // Additional languages
    let languageCodes = []

    for languageCode in languageCodes do
        printfn "Rendering /%s docs" languageCode
        let languageDir = DirectoryInfo(Path.Join(docsDir.FullName, languageCode))
        let languageBuildDir = DirectoryInfo(Path.Join(docsBuildDir.FullName, languageCode))
        Docs.build (languageDir.GetFiles()) languageBuildDir

    0

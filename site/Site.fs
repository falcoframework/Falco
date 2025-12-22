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
            _h3 [] [ _text "Project Links" ]
            _a [ _href_ "/"] [ _text "Project Homepage" ]
            _a [ _class_ "db"; _href_ "https://github.com/FalcoFramework/Falco"; _targetBlank_ ]
                [ _text "Source Code" ]
            _a [ _class_ "db"; _href_ "https://github.com/FalcoFramework/Falco/issues"; _targetBlank_ ]
                [ _text "Issue Tracker" ]
            _a [ _class_ "db"; _href_ "https://github.com/FalcoFramework/Falco/discussions"; _targetBlank_ ]
                [ _text "Discussion" ]
            _a [ _class_ "db"; _href_ "https://twitter.com/falco_framework"; _targetBlank_ ]
                [ _text "Twitter" ]
        ]

    let docsNav =
        [
            Text.h3 "Contents"
            _ul [ _class_ "nl3 f6" ] [
                _li [] [ _a [ _href_ "get-started.html" ] [ _text "Getting Started" ] ]
                _li [] [ _a [ _href_ "routing.html" ] [ _text "Routing" ] ]
                _li [] [ _a [ _href_ "response.html" ] [ _text "Response Writing" ] ]
                _li [] [ _a [ _href_ "request.html" ] [ _text "Request Handling" ] ]
                _li [] [ _a [ _href_ "markup.html" ] [ _text "Markup" ] ]
                _li [] [
                    _text "Security"
                    _ul [] [
                        _li [] [ _a [ _href_ "cross-site-request-forgery.html" ] [ _text "Cross Site Request Forgery (XSRF)" ] ]
                        _li [] [ _a [ _href_ "authentication.html" ] [ _text "Authentication & Authorization" ] ]
                    ]
                ]
                _li [] [ _a [ _href_ "host-configuration.html" ] [ _text "Host Configuration" ] ]
                _li [] [ _a [ _href_ "deployment.html" ] [ _text "Deployment" ] ]
                _li [] [
                    _text "Examples"
                    _ul [] [
                        _li [] [ _a [ _href_ "example-hello-world.html" ] [ _text "Hello World" ] ]
                        _li [] [ _a [ _href_ "example-hello-world-mvc.html" ] [ _text "Hello World MVC" ] ]
                        _li [] [ _a [ _href_ "example-dependency-injection.html" ] [ _text "Dependency Injection" ] ]
                        _li [] [ _a [ _href_ "example-external-view-engine.html" ] [ _text "External View Engine" ] ]
                        _li [] [ _a [ _href_ "example-basic-rest-api.html" ] [ _text "Basic REST API" ] ]
                        _li [] [ _a [ _href_ "example-open-api.html" ] [ _text "Open API" ] ]
                        _li [] [ _a [ _href_ "example-htmx.html" ] [ _text "htmx" ] ]
                    ]
                ]
                _li [] [ _a [ _href_ "migrating-from-v4-to-v5.html" ] [ _text "V5 Migration Guide" ] ]
            ]
        ]

    let private _layoutHead title =
        let title =
            if String.IsNullOrWhiteSpace(title) then
                "Falco - F# web toolkit for ASP.NET Core"
            else
                $"{title} - Falco Documentation"

        [
            _meta  [ _charset_ "UTF-8" ]
            _meta  [ _httpEquiv_ "X-UA-Compatible"; _content_ "IE=edge, chrome=1" ]
            _meta  [ _name_ "viewport"; _content_ "width=device-width, initial-scale=1" ]
            _title [] [ _text title ]
            _meta  [ _name_ "description"; _content_ "A functional-first toolkit for building brilliant ASP.NET Core applications using F#." ]

            _link [ _rel_ "shortcut icon"; _href_ "/favicon.ico"; _type_ "image/x-icon" ]
            _link [ _rel_ "icon"; _href_ "/favicon.ico"; _type_ "image/x-icon" ]
            _link [ _rel_ "preconnect"; _href_ "https://fonts.gstatic.com" ]
            _link [ _href_ "https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;700&display=swap"; _rel_ "stylesheet" ]
            _link [ _href_ "/prism.css"; _rel_ "stylesheet" ]
            _link [ _href_ "/tachyons.css"; _rel_ "stylesheet" ]
            _link [ _href_ "/style.css"; _rel_ "stylesheet" ]

            _script [ _async_; _src_ "https://www.googletagmanager.com/gtag/js?id=G-D62HSJHMNZ" ] []
            _script [] [ _text """window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', 'G-D62HSJHMNZ');"""
            ]
        ]

    let private _layoutFooter =
        _footer [ _class_ "cl pa3 bg-merlot" ] [
            _div [ _class_ "f7 tc white-70" ]
                [ _text $"&copy; 2020-{DateTime.Now.Year} Pim Brouwers & contributors." ]
        ]

    let layout (model : LayoutModel) =
        let topBar =
            _div [] [
                _nav [ _class_ "flex flex-column flex-row-l items-center" ] [
                    _a [ _href_ "/" ]
                        [ _img [ _src_ "/icon.svg"; _class_ "w3 pb3 pb0-l o-80 hover-o-100" ] ]
                    _div [ _class_ "flex-grow-1-l tc tr-l" ] [
                        _a [ _href_ "/docs"; _title_ "Overview of Falco's key features"; _class_ "dib mh2 mh3-l no-underline white-90 hover-white" ]
                            [ _text "docs" ]
                        _a [ _href_ "https://github.com/FalcoFramework/Falco"; _title_ "Fork Falco on GitHub"; _alt_ "Falco GitHub Link"; _targetBlank_; _class_ "dib mh2 ml3-l no-underline white-90 hover-white" ]
                            [ _text "code" ]
                        _a [ _href_ "https://github.com/FalcoFramework/Falco/tree/master/examples"; _title_ "Falco code samples"; _alt_ "Faclo code samples link"; _class_ "dib ml2 mh3-l no-underline white-90 hover-white" ]
                            [ _text "samples" ]
                        _a [ _href_ "https://github.com/FalcoFramework/Falco/discussions"; _title_ "Need help?"; _alt_ "Faclo GitHub discussions link"; _class_ "dib ml2 mh3-l no-underline white-90 hover-white" ]
                            [ _text "help" ]
                    ]
                ]
            ]

        let greeting =
            _div [ _class_ "mw6 center pb5 noto tc fw4 lh-copy white" ] [
                _h1 [ _class_ "mt4 mb3 fw4 f2" ]
                    [ _text "Meet Falco." ]
                _h2 [ _class_ "mt0 mb4 fw4 f4 f3-l" ]
                    [ _text "Falco is a toolkit for building fast and functional-first web applications using F#." ]

                _div [ _class_ "tc" ] [
                    _a [ _href_ "/docs/get-started.html"; _title_ "Learn how to get started using Falco"; _class_ "dib mh2 mb2 ph3 pv2 merlot bg-white ba b--white br2 no-underline" ]
                        [ _text "Get Started" ]
                    _a [ _href_ "#falco"; _class_ "dib mh2 ph3 pv2 white ba b--white br2 no-underline" ]
                        [ _text "Learn More" ]
                ]
            ]

        let releaseInfo =
            _div [ _class_ "mb4 bt b--white-20 tc lh-solid" ] [
                _a [ _href_ "https://www.nuget.org/packages/Falco"; _class_ "dib center ph1 ph4-l pv3 bg-merlot white no-underline ty--50"; _targetBlank_ ]
                    [ _text "Latest release: 5.2.0 (December, 21, 2025)" ]
            ]

        let benefits =
            _div [ _class_ "cf tc lh-copy" ] [
                _div [ _class_ "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    _img [ _src_ "/icons/fast.svg"; _class_ "w4 o-90" ]
                    _h3 [ _class_ "mv2 white" ]
                        [ _text "Fast & Lightweight" ]
                    _div [ _class_ "mb3 white-90" ]
                        [ _text "Optimized for speed and low memory usage." ]
                    _a [ _href_ "https://web-frameworks-benchmark.netlify.app/result?l=fsharp"; _targetBlank_; _class_ "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ _text "Learn More" ]
                 ]

                _div [ _class_ "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    _img [ _src_ "/icons/easy.svg"; _class_ "w4 o-90" ]
                    _h3 [ _class_ "mv2 white" ] [ _text "Easy to Learn" ]
                    _div [ _class_ "mb3 white-90" ] [ _text "Simple, predictable, and easy to pick up." ]
                    _a [ _href_ "/docs/get-started.html"; _title_ "Learn how to get started using Falco"; _class_ "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ _text "Get Started" ]
                 ]

                _div [ _class_ "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l br-l b--white-20" ] [
                    _img [ _src_ "/icons/view.svg"; _class_ "w4 o-90" ]
                    _h3 [ _class_ "mv2 white" ] [ _text "Native View Engine" ]
                    _div [ _class_ "mb3 white-90" ] [ _text "Markup is written in F# and compiled." ]
                    _a [ _href_ "/docs/markup.html"; _title_ "View examples of Falco markup module"; _class_ "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ _text "See Examples" ]
                 ]

                _div [ _class_ "fl-l mw5 mw-none-l w-25-l center mb4 ph4-l" ] [
                    _img [ _src_ "/icons/integrate.svg"; _class_ "w4 o-90" ]
                    _h3 [ _class_ "mv2 white" ] [ _text "Customizable" ]
                    _div [ _class_ "mb3 white-90" ] [ _text "Seamlessly integrates with ASP.NET." ]
                    _a [ _href_ "https://github.com/FalcoFramework/Falco/tree/master/samples/ScribanExample"; _targetBlank_; _title_ "Example of incorporating a third-party view engine"; _class_ "dib mh2 pa2 f6 white ba b--white br2 no-underline" ]
                        [ _text "Explore How" ]
                 ]
            ]

        _html [ _lang_ "en"; ] [
            _head [] (_layoutHead model.Title)
            _body [ _class_ "noto bg-merlot bg-dots bg-parallax" ] [
                _header [ _class_ "pv3" ] [
                    _div [ _class_ "mw8 center pa3" ] [
                        topBar
                        greeting
                        releaseInfo
                        benefits
                    ]
                ]

                _div [ _class_ "h100vh bg-white" ] [
                    _div [ _class_ "cf mw8 center pv4 ph3" ] [
                        _main [] [ _text model.MainContent ]
                    ]
                ]

                _layoutFooter

                _script [ _src_ "/prism.js" ] []
            ]
        ]

    let layoutTwoCol (model : LayoutTwoColModel) =
        _html [ Attr.lang "en"; ] [
            _head [] (_layoutHead model.Title)
            _body [ _class_ "noto lh-copy" ] [
                _div [ _class_ "min-vh-100 mw9 center pa3 overflow-hidden" ] [
                    _nav [ _class_ "sidebar w-20-l fl-l mb3 mb0-l" ] [
                        _div [ _class_ "flex items-center" ] [
                            _a [ _href_ "/docs"; _class_ "db w3 w4-l" ]
                                [ _img [ _src_ "/brand.svg"; _class_ "br3" ] ]
                            _h2 [ _class_ "dn-l mt3 ml3 fw4 gray" ]
                                [ _text "Falco Documentation" ]
                        ]
                        _div [ _class_ "dn db-l" ] model.SideContent
                    ]
                    _main [ _class_ "w-80-l fl-l pl3-l" ] [ _text model.MainContent ]
                ]
                _layoutFooter
                _script [ _src_ "/prism.js" ] []
            ]
        ]

module Docs =
    let build (docs : FileInfo[]) (buildDir : DirectoryInfo) =
        if not buildDir.Exists then buildDir.Create()

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
    let buildDirPath = DirectoryInfo(Path.Join(workingDir.FullName, "../docs"))
    printfn "Clearing build directory...\n  %s" buildDirPath.FullName

    if buildDirPath.Exists then
        for file in buildDirPath.EnumerateFiles("*.html", EnumerationOptions(RecurseSubdirectories = true)) do
            file.Delete()
    else
        buildDirPath.Create ()

    printfn "Rendering homepage..."
    let indexMarkdown = Path.Join(workingDir.FullName, "../README.md") |> File.ReadAllText
    let mainContent = Markdown.render indexMarkdown
    let mainWithoutTitle =
        Regex.Replace(mainContent.Body, "<h1.*?</h1>", "", RegexOptions.Singleline).Trim()
    { Title = String.Empty
      MainContent = mainWithoutTitle }
    |> View.layout
    |> renderHtml
    |> fun text -> File.WriteAllText(Path.Join(buildDirPath.FullName, "index.html"), text)

    let docsDir = DirectoryInfo(Path.Join(workingDir.FullName, "../documentation"))
    let docsBuildDir = DirectoryInfo(Path.Join(buildDirPath.FullName, "../docs/docs"))
    printfn "Rendering docs...\n  From: %s\n  To:   %s" docsDir.FullName docsBuildDir.FullName
    Docs.build (docsDir.GetFiles "*.md") docsBuildDir

    // Additional languages
    let languageCodes = []

    for languageCode in languageCodes do
        printfn "Rendering /%s docs" languageCode
        let languageDir = DirectoryInfo(Path.Join(docsDir.FullName, languageCode))
        let languageBuildDir = DirectoryInfo(Path.Join(docsBuildDir.FullName, languageCode))
        Docs.build (languageDir.GetFiles()) languageBuildDir

    0

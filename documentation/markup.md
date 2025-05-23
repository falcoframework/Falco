# Markup

Falco.Markup is broken down into three primary modules, `Elem`, `Attr` and `Text`, which are used to generate elements, attributes and text nodes respectively. Each module contain a suite of functions mapping to the various element/attribute/node names. But can also be extended to create custom elements and attributes.

Primary elements are broken down into two types, `ParentNode` or `SelfClosingNode`.

`ParentNode` elements are those that can contain other elements. Represented as functions that receive two inputs: attributes and optionally elements.

```fsharp
let markup =
    Elem.div [ Attr.class' "heading" ] [
        Text.h1 "Hello world!" ]
```

`SelfClosingNode` elements are self-closing tags. Represented as functions that receive one input: attributes.

```fsharp
let markup =
    Elem.div [ Attr.class' "divider" ] [
        Elem.hr [] ]
```

Text is represented using the `TextNode` and created using one of the functions in the `Text` module.

```fsharp
let markup =
    Elem.div [] [
        Text.comment "An HTML comment"
        Text.p "A paragraph"
        Elem.p [] [ Text.rawf "Hello %s" "Jim" ]
        Elem.code [] [ Text.enc "<div>Hello</div>" ] // HTML encodes text before rendering
    ]
```

Attributes contain two subtypes as well, `KeyValueAttr` which represent key/value attributes or `NonValueAttr` which represent boolean attributes.

```fsharp
let markup =
    Elem.input [ Attr.type' "text"; Attr.required ]
```

Most [JavaScript Events](https://developer.mozilla.org/en-US/docs/Web/Events) have also been mapped in the `Attr` module. All of these events are prefixed with the word "on" (i.e., `Attr.onclick`, `Attr.onfocus` etc.)

```fsharp
let markup =
    Elem.button [ Attr.onclick "console.log(\"hello world\")" ] [ Text.raw "Click me" ]
```

## HTML

Though Falco.Markup can be used to produce any markup. It is first and foremost an HTML library.

### Combining views to create complex output

```fsharp
open Falco.Markup

// Components
let divider =
    Elem.hr [ Attr.class' "divider" ]

// Template
let master (title : string) (content : XmlNode list) =
    Elem.html [ Attr.lang "en" ] [
        Elem.head [] [
            Elem.title [] [ Text.raw title ]
        ]
        Elem.body [] content
    ]

// Views
let homeView =
    master "Homepage" [
        Text.h1 "Homepage"
        divider
        Text.p "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]

let aboutView =
    master "About Us" [
        Text.h1 "About"
        divider
        Text.p "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]
```

### Strongly-typed views

```fsharp
open Falco.Markup

type Person =
    { FirstName : string
      LastName : string }

let doc (person : Person) =
    Elem.html [ Attr.lang "en" ] [
        Elem.head [] [
            Elem.title [] [ Text.raw "Sample App" ]
        ]
        Elem.body [] [
            Elem.main [] [
                Text.h1 "Sample App"
                Text.p $"{person.First} {person.Last}"
            ]
        ]
    ]
```

### Forms

Forms are the lifeblood of HTML applications. A basic form using the markup module would like the following:

```fsharp
let dt = DateTime.Now

Elem.form [ Attr.methodPost; Attr.action "/submit" ] [
    Elem.label [ Attr.for' "name" ] [ Text.raw "Name" ]
    Elem.input [ Attr.id "name"; Attr.name "name"; Attr.typeText ]

    Elem.label [ Attr.for' "birthdate" ] [ Text.raw "Birthday" ]
    Elem.input [ Attr.id "birthdate"; Attr.name "birthdate"; Attr.typeDate; Attr.valueDate dt ]

    Elem.input [ Attr.typeSubmit ]
]
```

Expanding on this, we can create a more complex form involving multiple inputs and input types as follows:

```fsharp
Elem.form [ Attr.method "post"; Attr.action "/submit" ] [
    Elem.label [ Attr.for' "name" ] [ Text.raw "Name" ]
    Elem.input [ Attr.id "name"; Attr.name "name" ]

    Elem.label [ Attr.for' "bio" ] [ Text.raw "Bio" ]
    Elem.textarea [ Attr.name "id"; Attr.name "bio" ] []

    Elem.label [ Attr.for' "hobbies" ] [ Text.raw "Hobbies" ]
    Elem.select [ Attr.id "hobbies"; Attr.name "hobbies"; Attr.multiple ] [
        Elem.option [ Attr.value "programming" ] [ Text.raw "Programming" ]
        Elem.option [ Attr.value "diy" ] [ Text.raw "DIY" ]
        Elem.option [ Attr.value "basketball" ] [ Text.raw "Basketball" ]
    ]

    Elem.fieldset [] [
        Elem.legend [] [ Text.raw "Do you like chocolate?" ]
        Elem.label [] [
            Text.raw "Yes"
            Elem.input [ Attr.typeRadio; Attr.name "chocolate"; Attr.value "yes" ] ]
        Elem.label [] [
            Text.raw "No"
            Elem.input [ Attr.typeRadio; Attr.name "chocolate"; Attr.value "no" ] ]
    ]

    Elem.fieldset [] [
        Elem.legend [] [ Text.raw "Subscribe to our newsletter" ]
        Elem.label [] [
            Text.raw "Receive updates about product"
            Elem.input [ Attr.typeCheckbox; Attr.name "newsletter"; Attr.value "product" ] ]
        Elem.label [] [
            Text.raw "Receive updates about company"
            Elem.input [ Attr.typeCheckbox; Attr.name "newsletter"; Attr.value "company" ] ]
    ]

    Elem.input [ Attr.typeSubmit ]
]
```

A simple but useful _meta_-element `Elem.control` can reduce the verbosity required to create form outputs. The same form would look like:

```fsharp
Elem.form [ Attr.method "post"; Attr.action "/submit" ] [
    Elem.control "name" [] [ Text.raw "Name" ]

    Elem.controlTextarea "bio" [] [ Text.raw "Bio" ] []

    Elem.controlSelect "hobbies" [ Attr.multiple ] [ Text.raw "Hobbies" ] [
        Elem.option [ Attr.value "programming" ] [ Text.raw "Programming" ]
        Elem.option [ Attr.value "diy" ] [ Text.raw "DIY" ]
        Elem.option [ Attr.value "basketball" ] [ Text.raw "Basketball" ]
    ]

    Elem.fieldset [] [
        Elem.legend [] [ Text.raw "Do you like chocolate?" ]
        Elem.control "chocolate" [ Attr.id "chocolate_yes"; Attr.typeRadio ] [ Text.raw "yes" ]
        Elem.control "chocolate" [ Attr.id "chocolate_no"; Attr.typeRadio ] [ Text.raw "no" ]
    ]

    Elem.fieldset [] [
        Elem.legend [] [ Text.raw "Subscribe to our newsletter" ]
        Elem.control "newsletter" [ Attr.id "newsletter_product"; Attr.typeCheckbox ] [ Text.raw "Receive updates about product" ]
        Elem.control "newsletter" [ Attr.id "newsletter_company"; Attr.typeCheckbox ] [ Text.raw "Receive updates about company" ]
    ]

    Elem.input [ Attr.typeSubmit ]
]
```

### Attribute Value

One of the more common places of sytanctic complexity is with `Attr.value` which expects, like all `Attr` functions, `string` input. Some helpers exist to simplify this.

```fsharp
let dt = DateTime.Now

Elem.input [ Attr.typeDate; Attr.valueStringf "yyyy-MM-dd" dt ]

// you could also just use:
Elem.input [ Attr.typeDate; Attr.valueDate dt ] // formatted to ISO-8601 yyyy-MM-dd

// or,
Elem.input [ Attr.typeMonth; Attr.valueMonth dt ] // formatted to ISO-8601 yyyy-MM

// or,
Elem.input [ Attr.typeWeek; Attr.valueWeek dt ] // formatted to Gregorian yyyy-W#

// it works for TimeSpan too:
let ts = TimeSpan(12,12,0)
Elem.input [ Attr.typeTime; Attr.valueTime ts ] // formatted to hh:mm

// there is a helper for Option too:
let someTs = Some ts
Elem.input [ Attr.typeTime; Attr.valueOption Attr.valueTime someTs ]
```

### Merging Attributes

The markup module allows you to easily create components, an excellent way to reduce code repetition in your UI. To support runtime customization, it is advisable to ensure components (or reusable markup blocks) retain a similar function "shape" to standard elements. That being, `XmlAttribute list -> XmlNode list -> XmlNode`.

This means that you will inevitably end up needing to combine your predefined `XmlAttribute list` with a list provided at runtime. To facilitate this, the `Attr.merge` function will group attributes by key, and intelligently concatenate the values in the case of additive attributes (i.e., `class`, `style` and `accept`).

```fsharp
open Falco.Markup

// Components
let heading (attrs : XmlAttribute list) (content : XmlNode list) =
    // safely combine the default XmlAttribute list with those provided
    // at runtime
    let attrs' =
        Attr.merge [ Attr.class' "text-large" ] attrs

    Elem.div [] [
        Elem.h1 [ attrs' ] content
    ]

// Template
let master (title : string) (content : XmlNode list) =
    Elem.html [ Attr.lang "en" ] [
        Elem.head [] [
            Elem.title [] [ Text.raw title ]
        ]
        Elem.body [] content
    ]

// Views
let homepage =
    master "Homepage" [
        heading [ Attr.class' "red" ] [ Text.raw "Welcome to the homepage" ]
        Text.p "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]

let homepage =
    master "About Us" [
        heading [ Attr.class' "purple" ] [ Text.raw "This is what we're all about" ]
        Text.p "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]
```

## Custom Elements & Attributes

Every effort has been taken to ensure the HTML and SVG specs are mapped to functions in the module. In the event an element or attribute you need is missing, you can either file an [issue](https://github.com/FalcoFramework/Falco.Markup/issues), or more simply extend the module in your project.

An example creating custom XML elements and using them to create a structured XML document:

```fsharp
open Falco.Makrup

module Elem =
    let books = Elem.create "books"
    let book = Elem.create "book"
    let name = Elem.create "name"

module Attr =
    let soldOut = Attr.createBool "soldOut"

let xmlDoc =
    Elem.books [] [
        Elem.book [ Attr.soldOut ] [
            Elem.name [] [ Text.raw "To Kill A Mockingbird" ]
        ]
    ]

let xml = renderXml xmlDoc
```

## SVG

Much of the SVG spec has been mapped to element and attributes functions. There is also an SVG template to help initialize a new drawing with a valid viewbox.

```fsharp
open Falco.Markup
open Falco.Markup.Svg

// https://developer.mozilla.org/en-US/docs/Web/SVG/Element/text#example
let svgDrawing =
    Templates.svg (0, 0, 240, 80) [
        Elem.style [] [
            Text.raw ".small { font: italic 13px sans-serif; }"
            Text.raw ".heavy { font: bold 30px sans-serif; }"
            Text.raw ".Rrrrr { font: italic 40px serif; fill: red; }"
        ]
        Elem.text [ Attr.x "20"; Attr.y "35"; Attr.class' "small" ] [ Text.raw "My" ]
        Elem.text [ Attr.x "40"; Attr.y "35"; Attr.class' "heavy" ] [ Text.raw "cat" ]
        Elem.text [ Attr.x "55"; Attr.y "55"; Attr.class' "small" ] [ Text.raw "is" ]
        Elem.text [ Attr.x "65"; Attr.y "55"; Attr.class' "Rrrrr" ] [ Text.raw "Grumpy!" ]
    ]

let svg = renderNode svgDrawing
```

[Next: Cross-site Request Forgery (XSRF)](cross-site-request-forgery.md)

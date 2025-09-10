# Markup

Falco.Markup is broken down into three primary modules, `Elem`, `Attr` and `Text`, which are used to generate elements, attributes and text nodes respectively. Each module contain a suite of functions mapping to the various element/attribute/node names. But can also be extended to create custom elements and attributes.

Primary elements are broken down into two types, `ParentNode` or `SelfClosingNode`.

`ParentNode` elements are those that can contain other elements. Represented as functions that receive two inputs: attributes and optionally elements.

Each of the primary modules can be access using the name directly, or using the "underscore syntax" seen below.

| Module | Syntax |
|--------|--------|
| `Elem` | `_h1 [] []` |
| `Attr` | `_class_ "my-class"` |
| `Text` | `_text "Hello world!"` |
| `Text` shortcuts | `_h1' "Hello world"` (note the trailing apostrophe) |


```fsharp
let markup =
    _div [ _class_ "heading" ] [
        _h1' "Hello world!" ]
```

`SelfClosingNode` elements are self-closing tags. Represented as functions that receive one input: attributes.

```fsharp
let markup =
    _div [ _class_ "divider" ] [
        _hr [] ]
```

Text is represented using the `TextNode` and created using one of the functions in the `Text` module.

```fsharp
let markup =
    _div [] [
        _p' "A paragraph"
        _p [] [ _textf "Hello %s" "Jim" ]
        _code [] [ _textEnc "<div>Hello</div>" ] // HTML encodes text before rendering
    ]
```

Attributes contain two subtypes as well, `KeyValueAttr` which represent key/value attributes or `NonValueAttr` which represent boolean attributes.

```fsharp
let markup =
    _input [ _type_ "text"; _required_ ]
```

Most [JavaScript Events](https://developer.mozilla.org/en-US/docs/Web/Events) have also been mapped in the `Attr` module. All of these events are prefixed with the word "on" (i.e., `_onclick_`, `_onfocus_` etc.)

```fsharp
let markup =
    _button [ _onclick_ "console.log(\"hello world\")" ] [ _text "Click me" ]
```

## HTML

Though Falco.Markup can be used to produce any markup. It is first and foremost an HTML library.

### Combining views to create complex output

```fsharp
open Falco.Markup

// Components
let divider =
    _hr [ _class_ "divider" ]

// Template
let master (title : string) (content : XmlNode list) =
    _html [ _lang_ "en" ] [
        _head [] [
            _title [] [ _text title ]
        ]
        _body [] content
    ]

// Views
let homeView =
    master "Homepage" [
        _h1' "Homepage"
        divider
        _p' "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]

let aboutView =
    master "About Us" [
        _h1' "About"
        divider
        _p' "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]
```

### Strongly-typed views

```fsharp
open Falco.Markup

type Person =
    { FirstName : string
      LastName : string }

let doc (person : Person) =
    _html [ _lang_ "en" ] [
        _head [] [
            _title [] [ _text "Sample App" ]
        ]
        _body [] [
            _main [] [
                _h1' "Sample App"
                _p' $"{person.First} {person.Last}"
            ]
        ]
    ]
```

### Forms

Forms are the lifeblood of HTML applications. A basic form using the markup module would like the following:

```fsharp
let dt = DateTime.Now

_form [ _methodPost_; _action_ "/submit" ] [
    _label [ _for_' "name" ] [ _text "Name" ]
    _input [ _id_ "name"; _name_ "name"; _typeText_ ]

    _label [ _for_' "birthdate" ] [ _text "Birthday" ]
    _input [ _id_ "birthdate"; _name_ "birthdate"; _typeDate_; _valueDate_ dt ]

    _input [ _typeSubmit_ ]
]
```

Expanding on this, we can create a more complex form involving multiple inputs and input types as follows:

```fsharp
_form [ _methodPost_; _action_ "/submit" ] [
    _label [ _for_' "name" ] [ _text "Name" ]
    _input [ _id_ "name"; _name_ "name" ]

    _label [ _for_' "bio" ] [ _text "Bio" ]
    _textarea [ _name_ "id"; _name_ "bio" ] []

    _label [ _for_' "hobbies" ] [ _text "Hobbies" ]
    _select [ _id_ "hobbies"; _name_ "hobbies"; _multiple_ ] [
        _option [ _value_ "programming" ] [ _text "Programming" ]
        _option [ _value_ "diy" ] [ _text "DIY" ]
        _option [ _value_ "basketball" ] [ _text "Basketball" ]
    ]

    _fieldset [] [
        _legend [] [ _text "Do you like chocolate?" ]
        _label [] [
            _text "Yes"
            _input [ _typeRadio_; _name_ "chocolate"; _value_ "yes" ] ]
        _label [] [
            _text "No"
            _input [ _typeRadio_; _name_ "chocolate"; _value_ "no" ] ]
    ]

    _fieldset [] [
        _legend [] [ _text "Subscribe to our newsletter" ]
        _label [] [
            _text "Receive updates about product"
            _input [ _typeCheckbox_; _name_ "newsletter"; _value_ "product" ] ]
        _label [] [
            _text "Receive updates about company"
            _input [ _typeCheckbox_; _name_ "newsletter"; _value_ "company" ] ]
    ]

    _input [ _typeSubmit_ ]
]
```

A simple but useful _meta_-element `_control` can reduce the verbosity required to create form outputs. The same form would look like:

```fsharp
_form [ _methodPost_; _action_ "/submit" ] [
    _control "name" [] [ _text "Name" ]

    _controlTextarea "bio" [] [ _text "Bio" ] []

    _controlSelect "hobbies" [ _multiple_ ] [ _text "Hobbies" ] [
        _option [ _value_ "programming" ] [ _text "Programming" ]
        _option [ _value_ "diy" ] [ _text "DIY" ]
        _option [ _value_ "basketball" ] [ _text "Basketball" ]
    ]

    _fieldset [] [
        _legend [] [ _text "Do you like chocolate?" ]
        _control "chocolate" [ _id_ "chocolate_yes"; _typeRadio_ ] [ _text "yes" ]
        _control "chocolate" [ _id_ "chocolate_no"; _typeRadio_ ] [ _text "no" ]
    ]

    _fieldset [] [
        _legend [] [ _text "Subscribe to our newsletter" ]
        _control "newsletter" [ _id_ "newsletter_product"; _typeCheckbox_ ] [ _text "Receive updates about product" ]
        _control "newsletter" [ _id_ "newsletter_company"; _typeCheckbox_ ] [ _text "Receive updates about company" ]
    ]

    _input [ _typeSubmit_ ]
]
```

### Attribute Value

One of the more common places of sytanctic complexity is with `_value_` which expects, like all `Attr` functions, `string` input. Some helpers exist to simplify this.

```fsharp
let dt = DateTime.Now

_input [ _typeDate_; _valueStringf_ "yyyy-MM-dd" dt ]

// you could also just use:
_input [ _typeDate_; _valueDate_ dt ] // formatted to ISO-8601 yyyy-MM-dd

// or,
_input [ _typeMonth_; _valueMonth_ dt ] // formatted to ISO-8601 yyyy-MM

// or,
_input [ _typeWeek_; _valueWeek_ dt ] // formatted to Gregorian yyyy-W#

// it works for TimeSpan too:
let ts = TimeSpan(12,12,0)
_input [ _typeTime_; _valueTime_ ts ] // formatted to hh:mm

// there is a helper for Option too:
let someTs = Some ts
_input [ _typeTime_; _valueOption_ _valueTime_ someTs ]
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
        Attr.merge [ _class_ "text-large" ] attrs

    _div [] [
        _h1 [ attrs' ] content
    ]

// Template
let master (title : string) (content : XmlNode list) =
    _html [ _lang_ "en" ] [
        _head [] [
            _title [] [ _text title ]
        ]
        _body [] content
    ]

// Views
let homepage =
    master "Homepage" [
        heading [ _class_ "red" ] [ _text "Welcome to the homepage" ]
        _p' "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]

let homepage =
    master "About Us" [
        heading [ _class_ "purple" ] [ _text "This is what we're all about" ]
        _p' "Lorem ipsum dolor sit amet, consectetur adipiscing."
    ]
```

## Custom Elements & Attributes

Every effort has been taken to ensure the HTML and SVG specs are mapped to functions in the module. In the event an element or attribute you need is missing, you can either file an [issue](https://github.com/pimbrouwers/Falco.Markup/issues), or more simply extend the module in your project.

An example creating custom XML elements and using them to create a structured XML document:

```fsharp
open Falco.Makrup

module XmlElem =
    let books = Attr.create "books"
    let book = Attr.create "book"
    let name = Attr.create "name"

module XmlAttr =
    let soldOut = Attr.createBool "soldOut"

let xmlDoc =
    XmlElem.books [] [
        XmlElem.book [ XmlAttr.soldOut ] [
            XmlElem.name [] [ _text "To Kill A Mockingbird" ]
        ]
    ]

let xml = renderXml xmlDoc
```

## Template Fragments

There are circumstances where you may want to render only a portion of your view. Especially common in [hypermedia driven](https://htmx.org/essays/hypermedia-driven-applications/) applications. Supporting [template fragments](https://htmx.org/essays/template-fragments/) is helpful in maintaining locality of behaviour, because it allows you to decompose a particular view for partial updates internally without pulling fragments of the template out to separate files for rendering, creating a large number of individual templates.

Falco.Markup supports this pattern by way of the `renderFragment` function, which will traverse the provided `XmlNode` tree and render only the child node matching the provided `id`. Otherwise, gracefully returning an empty string if no match is found.

```fsharp
open Falco.Markup

let view =
    _div [ _id_ "my-div"; _class_ "my-class" ] [
        _h1 [ _id_ "my-heading" ] [ _text "hello" ] ]

let render = renderFragment doc "my-heading"
// produces: <h1 id="my-heading">hello</h1>
```

## SVG

Much of the SVG spec has been mapped to element and attributes functions. There is also an SVG template to help initialize a new drawing with a valid viewbox.

```fsharp
open Falco.Markup
open Falco.Markup.Svg

// https://developer.mozilla.org/en-US/docs/Web/SVG/Element/text#example
let svgDrawing =
    Templates.svg (0, 0, 240, 80) [
        _style [] [
            _text ".small { font: italic 13px sans-serif; }"
            _text ".heavy { font: bold 30px sans-serif; }"
            _text ".Rrrrr { font: italic 40px serif; fill: red; }"
        ]
        _text [ _x_ "20"; _y_ "35"; _class_ "small" ] [ _text "My" ]
        _text [ _x_ "40"; _y_ "35"; _class_ "heavy" ] [ _text "cat" ]
        _text [ _x_ "55"; _y_ "55"; _class_ "small" ] [ _text "is" ]
        _text [ _x_ "65"; _y_ "55"; _class_ "Rrrrr" ] [ _text "Grumpy!" ]
    ]

let svg = renderNode svgDrawing
```

[Next: Cross-site Request Forgery (XSRF)](cross-site-request-forgery.md)

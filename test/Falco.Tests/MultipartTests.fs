module Falco.Tests.Multipart

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Falco
open Falco.Multipart
open FsUnit.Xunit
open Xunit
open Microsoft.AspNetCore.WebUtilities
open Microsoft.Extensions.Primitives

[<Fact>]
let ``MultipartReader.StreamSectionsAsync()`` () =
    let onePartBody =
        "--9051914041544843365972754266\r\n" +
        "Content-Disposition: form-data; name=\"name\"\r\n" +
        "\r\n" +
        "falco\r\n" +
        "--9051914041544843365972754266--\r\n";

    use body = new MemoryStream(Encoding.UTF8.GetBytes(onePartBody))

    let rd = new MultipartReader("9051914041544843365972754266", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize) // 10mb max file size
        form.Files.Count |> should equal 0

        let formData = FormData(RequestValue.parseForm(form, None), Some form.Files)
        let requestValue = formData?name.AsString()
        requestValue |> should equal "falco"
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() with 3-part body`` () =
    let threePartBody =
            "--9051914041544843365972754266\r\n" +
            "Content-Disposition: form-data; name=\"name\"\r\n" +
            "\r\n" +
            "falco\r\n" +
            "--9051914041544843365972754266\r\n" +
            "Content-Disposition: form-data; name=\"file1\"; filename=\"a.txt\"\r\n" +
            "Content-Type: text/plain\r\n" +
            "\r\n" +
            "Content of a.txt.\r\n" +
            "\r\n" +
            "--9051914041544843365972754266\r\n" +
            "Content-Disposition: form-data; name=\"file2\"; filename=\"a.html\"\r\n" +
            "Content-Type: text/html\r\n" +
            "\r\n" +
            "<!DOCTYPE html><title>Content of a.html.</title>\r\n" +
            "\r\n" +
            "--9051914041544843365972754266--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(threePartBody))

    let rd = new MultipartReader("9051914041544843365972754266", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize) // 10mb max file size
        form.Files.Count |> should equal 2

        // can we access the files?
        use ms = new MemoryStream()
        use st1 = form.Files.[0].OpenReadStream()
        st1.CopyTo(ms)

        ms.SetLength(0)
        use st2 = form.Files.[1].OpenReadStream()
        st1.CopyTo(ms)

        let formData = FormData(RequestValue.parseForm(form, None), Some form.Files)
        let requestValue = formData?name.AsString()
        requestValue |> should equal "falco"
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should reject file exceeding max size`` () =
    let largeFileBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"file\"; filename=\"large.txt\"\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        (String.replicate (11 * 1024 * 1024) "x") + "\r\n" +  // Actually 11MB
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(largeFileBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let maxSize = 10L * 1024L * 1024L  // 10MB max

        // Should throw InvalidOperationException
        let! ex = Assert.ThrowsAsync<InvalidOperationException>(
            fun () -> rd.StreamSectionsAsync(tokenSource.Token, maxSize) :> Task)

        ex.Message.Contains("exceeds maximum size") |> should equal true
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should handle empty form`` () =
    let emptyBody = "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(emptyBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        form.Count |> should equal 0
        form.Files.Count |> should equal 0
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should handle multiple form fields`` () =
    let multiFieldBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"field1\"\r\n" +
        "\r\n" +
        "value1\r\n" +
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"field2\"\r\n" +
        "\r\n" +
        "value2\r\n" +
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"field3\"\r\n" +
        "\r\n" +
        "value3\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(multiFieldBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        form.Count |> should equal 3
        form["field1"] |> should equal (StringValues("value1"))
        form["field2"] |> should equal (StringValues("value2"))
        form["field3"] |> should equal (StringValues("value3"))
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should handle duplicate field names`` () =
    let duplicateBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"tags\"\r\n" +
        "\r\n" +
        "tag1\r\n" +
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"tags\"\r\n" +
        "\r\n" +
        "tag2\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(duplicateBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        form["tags"].Count |> should equal 2
        form["tags"].[0] |> should equal "tag1"
        form["tags"].[1] |> should equal "tag2"
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should handle mixed files and fields`` () =
    let mixedBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"username\"\r\n" +
        "\r\n" +
        "john_doe\r\n" +
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"avatar\"; filename=\"avatar.png\"\r\n" +
        "Content-Type: image/png\r\n" +
        "\r\n" +
        "PNG_DATA_HERE\r\n" +
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"bio\"\r\n" +
        "\r\n" +
        "A short bio\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(mixedBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        form.Count |> should equal 2  // username, bio
        form.Files.Count |> should equal 1
        form["username"] |> should equal (StringValues("john_doe"))
        form["bio"] |> should equal (StringValues("A short bio"))
        form.Files[0].FileName |> should equal "avatar.png"
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should preserve file content type`` () =
    let fileBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"document\"; filename=\"doc.json\"\r\n" +
        "Content-Type: application/json\r\n" +
        "\r\n" +
        "{\"key\":\"value\"}\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(fileBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        form.Files.Count |> should equal 1
        form.Files[0].ContentType |> should equal "application/json"
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should skip sections with missing name`` () =
    let malformedBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; filename=\"file.txt\"\r\n" +  // Missing name
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "content\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(malformedBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        // Should be skipped entirely
        form.Count |> should equal 0
        form.Files.Count |> should equal 0
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() should skip file sections with missing filename`` () =
    let malformedBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"file\"\r\n" +  // Missing filename
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "content\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(malformedBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, DefaultMaxFileSize)

        // Should be skipped (no filename = not a file)
        form.Files.Count |> should equal 0
    }

[<Fact>]
let ``MultipartReader.StreamSectionsAsync() respects custom maxFileSize parameter`` () =
    let fileBody =
        "--boundary\r\n" +
        "Content-Disposition: form-data; name=\"file\"; filename=\"test.txt\"\r\n" +
        "Content-Type: text/plain\r\n" +
        "\r\n" +
        "small content\r\n" +
        "--boundary--\r\n"

    use body = new MemoryStream(Encoding.UTF8.GetBytes(fileBody))
    let rd = new MultipartReader("boundary", body)

    task {
        use tokenSource = new CancellationTokenSource()
        let! form = rd.StreamSectionsAsync(tokenSource.Token, 1024L)  // 1KB max

        form.Files.Count |> should equal 1
        form.Files[0].Length |> should lessThan 1024L
    }

namespace Falco

open System
open System.IO
open System.Net
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.WebUtilities
open Microsoft.Extensions.Primitives
open Microsoft.Net.Http.Headers

module Multipart =
    [<Literal>]
    let DefaultMaxFileSize = 32L * 1024L * 1024L // 32mb

    type private MultipartSectionData =
        | NoMultipartData
        | FormValueData of key : string * value : string
        | FormFileData of FormFile

    type private MultipartSection with
        /// Attempts to obtain encoding from content type, default to UTF8.
        static member private GetEncodingFromContentType(section : MultipartSection) =
            match MediaTypeHeaderValue.TryParse(StringSegment(section.ContentType)) with
            | false, _     -> System.Text.Encoding.UTF8
            | true, parsed -> parsed.Encoding

        /// Safely obtains the content disposition header value.
        static member private TryGetContentDisposition(section : MultipartSection) =
            match ContentDispositionHeaderValue.TryParse(StringSegment(section.ContentDisposition)) with
            | false, _     -> None
            | true, parsed -> Some parsed

        member private x.StreamSectionAsync(ct : CancellationToken, maxFileSize : int64) =
            task {
                match MultipartSection.TryGetContentDisposition(x) with
                | Some cd when cd.IsFileDisposition() && cd.FileName.HasValue && cd.Name.HasValue  ->

                    let str = new MemoryStream()
                    // do! x.Body.CopyToAsync(str, ct)

                    // let safeFileName = WebUtility.HtmlEncode cd.FileName.Value
                    // let file = new FormFile(str, int64 0, str.Length, cd.Name.Value, safeFileName)
                    // Stream with size check to avoid unbounded memory growth

                    let mutable bytesRead = 0L
                    let buffer = Array.zeroCreate 65536 // 64KB chunks
                    let mutable shouldRead = true

                    while shouldRead do
                        let! count = x.Body.ReadAsync(buffer, 0, buffer.Length, ct)
                        match count with
                        | 0 -> shouldRead <- false
                        | n ->
                            bytesRead <- bytesRead + int64 n
                            if bytesRead > maxFileSize then
                                raise (InvalidOperationException $"File exceeds maximum size of {maxFileSize} bytes")
                            do! str.WriteAsync(buffer, 0, n, ct)

                    let safeFileName = WebUtility.HtmlEncode cd.FileName.Value
                    let file = new FormFile(str, int64 0, str.Length, cd.Name.Value, safeFileName)

                    // necessary to prevent null reference exception when setting
                    // properties below. See:
                    // https://github.com/dotnet/aspnetcore/blob/ca2238e75173d1f04ff0664c53dc443716a01b9d/src/Http/Http/src/FormFile.cs#L48
                    file.Headers <- new HeaderDictionary()

                    file.ContentType <- x.ContentType
                    file.ContentDisposition <- x.ContentDisposition

                    return FormFileData file

                | Some cd when cd.IsFormDisposition() && cd.Name.HasValue ->
                    let key = HeaderUtilities.RemoveQuotes(cd.Name).Value
                    let encoding = MultipartSection.GetEncodingFromContentType(x)
                    use str = new StreamReader(x.Body, encoding, true, 8192, true)
                    let! requestValue = str.ReadToEndAsync()

                    return FormValueData (key, requestValue)

                | Some _
                | None ->
                    return NoMultipartData
            }

    type MultipartReader with

        member x.StreamSectionsAsync(ct : CancellationToken, ?maxFileSize : int64) =
            task {
                let formData = new KeyValueAccumulator()
                let formFiles = new FormFileCollection()

                let mutable shouldContinue = true

                while shouldContinue do
                    let! section = x.ReadNextSectionAsync(ct)

                    match isNull section with
                    | true ->
                        shouldContinue <- false

                    | false ->
                        // default to 32mb max file size if not provided
                        let! sectionData = section.StreamSectionAsync(ct, defaultArg maxFileSize DefaultMaxFileSize)

                        match sectionData with
                        | FormFileData file          -> formFiles.Add(file)
                        | FormValueData (key, value) -> formData.Append(key, value)
                        | NoMultipartData            -> shouldContinue <- false

                let formCollection = FormCollection(formData.GetResults(), formFiles) :> IFormCollection
                return formCollection
            }

    type HttpRequest with
        /// Determines if the content type contains multipart.
        member internal x.IsMultipart () : bool =
            x.ContentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0

        member private x.GetBoundary() =
            // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
            // The spec at https://tools.ietf.org/html/rfc2046#section-5.1 states that 70 characters is a reasonable limit.
            let lengthLimit = 70
            let contentType = MediaTypeHeaderValue.Parse(StringSegment(x.ContentType))
            let boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
            match boundary with
            | b when isNull b -> None
            | b when b.Length > lengthLimit -> None
            | b -> Some b

        /// Attempts to stream the HttpRequest body into IFormCollection.
        member x.StreamFormAsync (ct : CancellationToken, ?maxFileSize : int64) : Task<IFormCollection> =
            task {
                match x.IsMultipart(), x.GetBoundary() with
                | true, Some boundary ->
                    let multipartReader = new MultipartReader(boundary, x.Body)

                    // default to 32mb max file size if not provided
                    let! formCollection = multipartReader.StreamSectionsAsync(ct, defaultArg maxFileSize DefaultMaxFileSize)
                    return formCollection

                | _, None
                | false, _ -> return FormCollection.Empty
            }

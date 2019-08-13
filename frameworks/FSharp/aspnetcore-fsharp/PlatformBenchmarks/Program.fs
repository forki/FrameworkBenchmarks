
module App.App

open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core
open System
open System.Text
open System.Net
open System.IO.Pipelines
open System.Buffers

let ApplicationName = "Kestrel Platform-Level Application in F#"

module Paths =
    let Plaintext = "/plaintext"

module Texts =

    let _eoh = Encoding.ASCII.GetBytes "\r\n\r\n" // End Of Headers
    let _http11OK = Encoding.ASCII.GetBytes "HTTP/1.1 200 OK\r\n"
    let _headerServer = Encoding.ASCII.GetBytes  "Server: Custom"
    let _headerContentTypeText = Encoding.ASCII.GetBytes "Content-Type: text/plain\r\n"
    let _headerContentLength = Encoding.ASCII.GetBytes "Content-Length: "
    let _plainTextBody = Encoding.ASCII.GetBytes "Hello, World!"
    let _plainTextBodyLength = _plainTextBody.Length.ToString() |> Encoding.ASCII.GetBytes


module Endpoints =

    let createPlaintext() =
        IPEndPoint(IPAddress.Loopback, 8080)

module Writers =
    type WriterAdapter(writer:PipeWriter) =
        struct
        end
            interface IBufferWriter<byte> with

                member __.Advance(count:int) = writer.Advance(count)
                member __.GetMemory(sizeHint:int) = writer.GetMemory(sizeHint)
                member __.GetSpan(sizeHint:int) = writer.GetSpan(sizeHint)



    let plainText(pipeWriter: PipeWriter) =
        let writer = WriterAdapter(pipeWriter) :> IBufferWriter<byte>

        // HTTP 1.1 OK
        writer.Write(ReadOnlySpan<byte> Texts._http11OK)

        // Server headers
        writer.Write(ReadOnlySpan<byte> Texts._headerServer)
        // Date header
        //writer.Write(DateHeader.HeaderBytes)

        // Content-Type header
        writer.Write(ReadOnlySpan<byte> Texts._headerContentTypeText)

        // Content-Length header
        writer.Write(ReadOnlySpan<byte> Texts._headerContentLength)
        writer.Write(ReadOnlySpan<byte> Texts._plainTextBodyLength)

        // End of headers
        writer.Write(ReadOnlySpan<byte> Texts._eoh)

        // Body
        writer.Write(ReadOnlySpan<byte> Texts._plainTextBody)
        // TODO: writer.Commit()


[<EntryPoint>]
let main args =
    Console.WriteLine(ApplicationName)
    Console.WriteLine(Paths.Plaintext)
    // TODO: DateHeader.SyncDateTimer()

    let host =
        WebHostBuilder()
            // TODO: .UseBenchmarksConfiguration(config)
            .UseKestrel()
            .Build()


    host.Run(fun (ctx:HttpContext) -> ctx.Response.BodyPipe <- plainText)  // ???
    0



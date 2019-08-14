
module App.App

open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http
open System
open Microsoft.AspNetCore.Connections
open System.Text
open System.Net
open System.IO.Pipelines
open System.Buffers
open System.Threading.Tasks
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Builder

let ApplicationName = "Kestrel Platform-Level Application in F#"

module Connection =

    type IHttpConnection =
        inherit IHttpHeadersHandler
        inherit IHttpRequestLineHandler

        abstract member Reader : PipeReader with get, set
        abstract member Writer : PipeWriter with get, set

        abstract member ExecuteAsync : unit -> Task


    type HttpApplication<'TConnection when 'TConnection :> IHttpConnection and 'TConnection : (new : unit -> 'TConnection)>() =
        member __.ExecuteAsync(connection:ConnectionContext) =
            let httpConnection = new 'TConnection()
            httpConnection.Reader <- connection.Transport.Input
            httpConnection.Writer <- connection.Transport.Output
            httpConnection.ExecuteAsync()

    let UseHttpApplication<'TConnection when 'TConnection :> IHttpConnection and 'TConnection : (new : unit -> 'TConnection)> (builder:IConnectionBuilder) =
        builder.Use(fun _next ->
            let httpApplication = new HttpApplication<'TConnection>()
            new ConnectionDelegate(httpApplication.ExecuteAsync))


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

module Application =
    type Startup() =
        member this.Configure(app:IApplicationBuilder) =
            ()

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

    type MyApp() =
        member val Reader = Unchecked.defaultof<PipeReader> with get, set
        member val Writer = Unchecked.defaultof<PipeWriter> with get, set


        interface Connection.IHttpConnection with
            member self.Reader with get () = self.Reader and set v = self.Reader <- v
            member self.Writer with get () = self.Writer and set v = self.Writer <- v


            override __.OnStartLine(method:HttpMethod, version:Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpVersion, target:Span<byte>, path: Span<byte>, query: Span<byte>, customMethod:Span<byte>, pathEncoded:bool) =
                ()

            override __.OnHeader(name:Span<byte>, value: Span<byte>) =
                ()

            override __.OnHeadersComplete() =
                ()

            member self.ExecuteAsync() =
                let t = task {
                    // let task = self.Reader.ReadAsync()
                    // if not task.IsCompleted then
                    //     let! _ =  self.Writer.FlushAsync()
                    //     ()


                    // let! result = task
                    // let buffer = result.Buffer
                    // let mutable isRunningInBuffer = true
                    // while isRunningInBuffer do
                    //     if buffer.IsEmpty then
                    //         isRunningInBuffer <- false
                    //     else
                    //         ()
                    plainText self.Writer
                    self.Writer.Complete()

                    // No more input or incomplete data, Advance the Reader
                    //self.Reader.AdvanceTo(buffer.Start, examined);
                }
                t :> Task


[<EntryPoint>]
let main args =
    Console.WriteLine(ApplicationName)
    Console.WriteLine(Paths.Plaintext)
    // TODO: DateHeader.SyncDateTimer()

    let host =
        WebHostBuilder()
            // TODO: .UseBenchmarksConfiguration(config)
            .UseKestrel(fun (options:KestrelServerOptions) ->
                let endPoint = Endpoints.createPlaintext()

                options.Listen(endPoint, (fun (listenOptions:ListenOptions) ->
                    let builder = listenOptions :> IConnectionBuilder
                    Connection.UseHttpApplication<Application.MyApp> builder |> ignore
                ))
            )
            .UseStartup<Application.Startup>()
            .Build()


    host.Run()
    0



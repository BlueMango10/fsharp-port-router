(*
    This script runs a server which takes incomming tcp connections and routes
    communications to other ports based on an initial identification message.
*)

open System
open System.Net
open System.Net.Sockets
open System.IO

let ADDRESS = IPAddress.Any
let PORT = 5000

let INNER_ADDRESS = "localhost"

let INNER_PORT_TIMEOUT = 10 * 1000
let BLOCK_SIZE = 1024

[<EntryPoint>]
let main argv =

    let listener = new TcpListener(ADDRESS, PORT)
    listener.Start()

    printfn "Listener started"

    let relayBytes (fromStream: NetworkStream) (toStream: NetworkStream) =
        let buffer = Array.zeroCreate BLOCK_SIZE
        let dataSize = fromStream.Read(buffer, 0, BLOCK_SIZE)
        toStream.Write(buffer, 0, dataSize)
        sprintf "%A" buffer

    while true do
        let client = listener.AcceptTcpClient()
        printfn "Client(%d) connected" (client.GetHashCode())
        async {
            (* Setup client stream *)
            let stream = client.GetStream()
            use reader = new StreamReader(stream, leaveOpen=true) // Used to recieve inner port

            (* Setup inner client for server communicaion *)
            let innerPort = 
                try
                    stream.ReadTimeout <- INNER_PORT_TIMEOUT
                    let innerPort = Some (reader.ReadLine() |> int)
                    stream.ReadTimeout <- Threading.Timeout.Infinite
                    innerPort
                with
                | :? FormatException
                | :? IOException -> None
            match innerPort with
            | None -> printfn "Client(%d) lost before providing inner port" (client.GetHashCode())
                      client.Close()
            | Some innerPort ->
                let innerClient = new TcpClient()
                if // if we successfully connect innerClient to the server
                    try
                        innerClient.Connect(INNER_ADDRESS, innerPort)
                        true
                    with
                    | :? SocketException -> printfn "Client(%d) failed to connect to Server(%d) (server doesn probably not exist)" (innerClient.GetHashCode()) innerPort
                                            client.Close()
                                            innerClient.Close()
                                            false
                then
                    reader.Dispose()
                    printfn "Client(%d) mapped to Server(%d)" (client.GetHashCode()) innerPort
                    
                    (* Setup inner client stream *)
                    let innerStream = innerClient.GetStream()
                    
                    let mutable doRelay = true
                    let stop() =
                        doRelay <- false
                        client.Close()
                        innerClient.Close()

                    async { // Relay client messages to server
                        while doRelay do
                            try
                                let msg = relayBytes stream innerStream
                                printfn "Client(%d) -> Server(%d): %s" (client.GetHashCode()) innerPort msg
                            with
                            | :? IOException -> printfn "Client(%d) lost" (client.GetHashCode())
                                                stop()
                    }
                    |> Async.Start

                    async { // Relay server messages to client
                        while doRelay do
                            try
                                let msg = relayBytes innerStream stream
                                printfn "Server(%d) -> Client(%d): %s" innerPort (client.GetHashCode()) msg
                            with
                            | :? IOException -> printfn "Server(%d) lost" innerPort
                                                stop()
                    }
                    |> Async.Start
        }
        |> Async.Start

    0
(*
    This script runs a server which takes incomming tcp connections and routes
    communications to other ports based on an initial identification message.
*)

open System
open System.Net
open System.Net.Sockets
open System.IO

let ADDRESS = IPAddress.Any
let DEFAULT_PORT = 5000

let INNER_ADDRESS = "localhost"

let INNER_PORT_TIMEOUT = 10 * 1000
let DEFAULT_BLOCK_SIZE = 1024 * 4 // BRULES is the biggest message that needs to be atomic. It is just below 4000 bytes (may change)

[<EntryPoint>]
let main argv =

    let port = if argv.Length > 0 then (int argv.[0]) else DEFAULT_PORT // Use argv[1] or default port
    let blockSize = if argv.Length > 1 then (int argv.[1]) else DEFAULT_BLOCK_SIZE // Use argv[1] or default port

    let listener = new TcpListener(ADDRESS, port)
    listener.Start()

    printfn "Listener started on port %d" port

    let relayBytes (fromStream: NetworkStream) (toStream: NetworkStream) =
        let buffer = Array.zeroCreate blockSize
        let dataSize = fromStream.Read(buffer, 0, blockSize)
        if dataSize <= 0 then raise (InvalidDataException("No data on stream"))
        toStream.Write(buffer, 0, dataSize)
        (dataSize, sprintf "%A" buffer)

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
                                let (size, msg) = relayBytes stream innerStream
                                printfn "Client(%d) -> Server(%d): %s (%d bytes)" (client.GetHashCode()) innerPort msg size
                            with
                            | :? InvalidDataException
                            | :? IOException -> printfn "Client(%d) lost" (client.GetHashCode())
                                                stop()
                    }
                    |> Async.Start

                    async { // Relay server messages to client
                        while doRelay do
                            try
                                let (size, msg) = relayBytes innerStream stream
                                printfn "Server(%d) -> Client(%d): %s (%d bytes)" (client.GetHashCode()) innerPort msg size
                            with
                            | :? InvalidDataException
                            | :? IOException -> printfn "Server(%d) lost" innerPort
                                                stop()
                    }
                    |> Async.Start
        }
        |> Async.Start

    0
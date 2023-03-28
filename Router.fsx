(*
    This script runs a server which takes incomming tcp connections and routes
    communications to other ports based on an initial identification message.
*)

open System.Net.Sockets
open System.Net
open System.IO

let ADDRESS = IPAddress.Any
let PORT = 5000

let INNER_ADDRESS = "localhost"

let listener = new TcpListener(ADDRESS, PORT)
listener.Start()

printfn "Listener started"

while true do
    let client = listener.AcceptTcpClient()
    printfn "Client(%d) connected" (client.GetHashCode())
    async {
        (* Setup client stream *)
        let stream = client.GetStream()
        let reader = new StreamReader(stream)
        let writer = new StreamWriter(stream)

        (* Setup inner client for server communicaion *)
        let innerPort = reader.ReadLine() |> int
        let innerClient = new TcpClient()
        innerClient.Connect(INNER_ADDRESS, innerPort)
        printfn "Client(%d) mapped to Server(%d)" (client.GetHashCode()) innerPort
        
        (* Setup inner client stream *)
        let innerStream = innerClient.GetStream()
        let innerReader = new StreamReader(innerStream)
        let innerWriter = new StreamWriter(innerStream)
        
        async { // Relay client messages to server
            while true do
                let msg = reader.ReadLine()
                printfn "Client(%d) -> Server(%d): %s" (client.GetHashCode()) innerPort msg
                innerWriter.WriteLine(msg)
                innerWriter.Flush()
        }
        |> Async.Start

        async { // Relay server messages to client
            while true do
                let msg = innerReader.ReadLine()
                printfn "Server(%d) -> Client(%d): %s" innerPort (client.GetHashCode()) msg
                writer.WriteLine(msg)
                writer.Flush()
        }
        |> Async.Start
    }
    |> Async.Start
(*
    A simple echo server for testing purposes. It is based on streams.
*)

open System.Net.Sockets
open System.Net
open System.IO

let ADDRESS = IPAddress.Any
printf "port: "
let port = System.Console.ReadLine() |> int

let listener = new TcpListener(ADDRESS, port)
listener.Start()

printfn "Listener started"

while true do
    let client = listener.AcceptTcpClient()
    printfn "Client %d connected" (client.GetHashCode())
    async {
        use stream = client.GetStream()
        use reader = new StreamReader(stream)
        use writer = new StreamWriter(stream)
        while true do
            let msg = reader.ReadLine()
            printfn "%d: %s" (client.GetHashCode()) msg
            writer.WriteLine(msg)
            writer.Flush()
    }
    |> Async.Start
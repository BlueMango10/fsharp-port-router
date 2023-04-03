open System.Net.Sockets
open System.IO

printf "address: "
let address = System.Console.ReadLine()
printf "port: "
let port = System.Console.ReadLine() |> int

let client = new TcpClient()
client.Connect(address, port)

let stream = client.GetStream()
let writer = new StreamWriter(stream)
let reader = new StreamReader(stream)

printf "inner port: "
let innerPort = System.Console.ReadLine()
writer.WriteLineAsync(innerPort)
writer.Flush()

async { // Recieve messages from server
    while true do
        let msg = reader.ReadLine()
        printf "\rserver: %s\n> " msg
}
|> Async.Start

while true do
    printf "> "
    let msg = System.Console.ReadLine()
    writer.WriteLine(msg)
    writer.Flush()
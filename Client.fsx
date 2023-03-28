open System.Net.Sockets
open System.IO

let ADDRESS = "localhost"
let PORT = 5000

let client = new TcpClient()
client.Connect(ADDRESS, PORT)

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
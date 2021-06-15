using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Text;  
using System.Threading;  
  
// State object for reading client data asynchronously  
public class StateObject
{
    // Size of receive buffer.  
    public const int BufferSize = 1024;

    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];

    // Received data string.
    public StringBuilder sb = new StringBuilder();

    // Client socket.
    public Socket workSocket = null;
}  
//パケットを受けるところ
public class AsynchronousSocketListener
{
    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);
    

    public static void StartListening()
    {
        // ソケットのローカルエンドポイントを確立します。 
        // リスナーを起動しているコンピュータのDNS名は「host.contoso.com」です。  
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());  
        IPAddress ipAddress = ipHostInfo.AddressList[0];  
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);  
  
        // TCP/IPソケットを作成します。 
        Socket listener = new Socket(ipAddress.AddressFamily,  
            SocketType.Stream, ProtocolType.Tcp );  
  
        // ソケットをローカルのエンドポイントにバインドし、受信する接続を待ちます。 
        listener.Bind(localEndPoint);  
        listener.Listen(100);  
  
        while (true) {
            // イベントをノンシグナリング状態にする。   
            allDone.Reset(); 
            
            // 接続を待ち受ける非同期ソケットを起動します。 
            Console.WriteLine("Waiting for a connection...");  
            listener.BeginAccept(
                AcceptCallback,  
                listener );  
            
            // 接続が完了するまで待ってから続行してください。 
            allDone.WaitOne();  
        }
    }

    public static void AcceptCallback(IAsyncResult ar)
    {

        try
        {
            // メインスレッドに継続するように信号を送ります。 
            allDone.Set();  
  
            // クライアントのリクエストを処理するソケットを取得します。 
            Socket listener = (Socket) ar.AsyncState;  
            Socket handler = listener.EndAccept(ar);  
  
            // Stateオブジェクトを作成します。
            StateObject state = new StateObject();  
            state.workSocket = handler;  
            handler.BeginReceive( state.buffer, 0, StateObject.BufferSize, 0,  
                ReadCallback, state);
            
            for (int i = 0; i < 10; i++)
            {
                Send(handler, i.ToString());
                Thread.Sleep(1000);
            }
            handler.Shutdown(SocketShutdown.Both);  
            handler.Close(); 
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        String content = String.Empty;  
  
        // 非同期ステートオブジェクトからステートオブジェクトとハンドラソケットを取得します。 
        StateObject state = (StateObject) ar.AsyncState;  
        Socket handler = state.workSocket;  
  
        //  クライアント・ソケットからデータを読み込みます。
        int bytesRead = handler.EndReceive(ar);  
        // まだまだデータがあるかもしれないので、これまでに受信したデータを保存しておきましょう。 
        byte[] bytes = state.buffer;
        state.sb.Append(Encoding.ASCII.GetString(  
            bytes, 0, bytesRead));  
  
        // ファイルの終わりのタグをチェックします。タグがない場合は、さらにデータを読み込みます。 
        content = state.sb.ToString();
        
        Console.WriteLine(content);
    }

    private static void Send(Socket handler, String data)
    {
        // 文字列データをASCIIエンコーディングでバイトデータに変換します。 
        byte[] byteData = Encoding.ASCII.GetBytes(data);  
  
        // リモートデバイスへのデータ送信を開始します。 
        handler.BeginSend(byteData, 0, byteData.Length, 0,  
            SendCallback, handler);  
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // stateオブジェクトからソケットを取得します。 
            Socket handler = (Socket) ar.AsyncState;  
  
            // リモートデバイスへのデータ送信完了  
            int bytesSent = handler.EndSend(ar);  
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());  
        }  
    }
}
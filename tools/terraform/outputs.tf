output "server_ip" {
  description = "Public IP of the moorestech server"
  value       = aws_eip.server_eip.public_ip
}

output "game_server_address" {
  description = "Game server connection address"
  value       = "${aws_eip.server_eip.public_ip}:11564"
}

output "ssh_command" {
  description = "SSH command to connect to the server"
  value       = "ssh -i ${path.module}/moorestech-server-key.pem ubuntu@${aws_eip.server_eip.public_ip}"
}

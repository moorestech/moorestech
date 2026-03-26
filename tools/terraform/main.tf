terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }
}

provider "aws" {
  region = var.region
}

# SSH鍵の自動生成
# Auto-generate SSH key pair
resource "tls_private_key" "server_key" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

resource "aws_key_pair" "server_key" {
  key_name   = "moorestech-server-key"
  public_key = tls_private_key.server_key.public_key_openssh
}

resource "local_file" "private_key" {
  content         = tls_private_key.server_key.private_key_pem
  filename        = "${path.module}/moorestech-server-key.pem"
  file_permission = "0600"
}

# Ubuntu 22.04 LTS AMI（GLIBC 2.35以上が必要）
# Ubuntu 22.04 LTS AMI (requires GLIBC 2.35+)
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# セキュリティグループ
# Security group: SSH + game server port
resource "aws_security_group" "server_sg" {
  name        = "moorestech-server-sg"
  description = "Allow SSH and game server traffic"

  ingress {
    description = "SSH"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "Game Server"
    from_port   = 11564
    to_port     = 11564
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# ローカルでLinux Dedicated Serverをビルド
# Build Linux Dedicated Server locally before provisioning
resource "null_resource" "build_server" {
  triggers = {
    always_run = timestamp()
  }

  provisioner "local-exec" {
    command     = "bash ${path.module}/build.sh"
    working_dir = path.module
  }
}

# EC2インスタンス
# EC2 instance for moorestech server
resource "aws_instance" "server" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  key_name               = aws_key_pair.server_key.key_name
  vpc_security_group_ids = [aws_security_group.server_sg.id]

  root_block_device {
    volume_size = 30
    volume_type = "gp3"
  }

  tags = {
    Name = "moorestech-server"
  }

  depends_on = [null_resource.build_server]
}

# 固定IP
# Elastic IP for stable address
resource "aws_eip" "server_eip" {
  instance = aws_instance.server.id
}

# ビルド成果物とModデータを転送・起動
# Transfer build artifacts + mod data, then start server
resource "null_resource" "deploy" {
  triggers = {
    instance_id = aws_instance.server.id
    always_run  = timestamp()
  }

  connection {
    type        = "ssh"
    host        = aws_eip.server_eip.public_ip
    user        = "ubuntu"
    private_key = tls_private_key.server_key.private_key_pem
  }

  # サーバー側のディレクトリ作成
  # Create directories on the remote server
  provisioner "remote-exec" {
    inline = [
      "mkdir -p ~/moorestech_server",
      "mkdir -p ~/game",
    ]
  }

  # デプロイスクリプトで転送・起動
  # Transfer and start via deploy script
  provisioner "local-exec" {
    command = "bash ${path.module}/deploy.sh ${aws_eip.server_eip.public_ip} ${path.module}/moorestech-server-key.pem"
  }

  depends_on = [aws_eip.server_eip, local_file.private_key]
}

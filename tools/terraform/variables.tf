variable "region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t3.micro"
}

variable "server_build_dir" {
  description = "Path to the dedicated server build output"
  type        = string
  default     = "../moorestech_server/Output_DedicatedServer_StandaloneLinux64"
}

variable "game_data_dir" {
  description = "Path to the game data directory (mods, map, config)"
  type        = string
  default     = "../../moorestech_master/server_v8"
}

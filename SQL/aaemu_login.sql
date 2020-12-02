/*
Navicat MySQL Data Transfer

Source Server         : aaemu
Source Server Version : 80012
Source Host           : localhost:3306
Source Database       : aaemu_login

Target Server Type    : MYSQL
Target Server Version : 80012
File Encoding         : 65001

Date: 2019-05-03 19:13:58
*/

SET FOREIGN_KEY_CHECKS=0;

-- ----------------------------
-- Table structure for game_servers
-- ----------------------------
DROP TABLE IF EXISTS `game_servers`;
CREATE TABLE `game_servers` (
  `id` tinyint(3) unsigned NOT NULL,
  `name` text NOT NULL,
  `host` varchar(128) NOT NULL,
  `port` int(11) NOT NULL,
  `hidden` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of game_servers
-- ----------------------------
INSERT INTO `game_servers` VALUES ('1', 'AAEmu.Game', '127.0.0.1', '1239', '0');

-- ----------------------------
-- Table structure for users
-- ----------------------------
DROP TABLE IF EXISTS `users`;
CREATE TABLE `users` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `username` varchar(32) NOT NULL,
  `password` text NOT NULL,
  `email` varchar(128) NOT NULL,
  `last_login` bigint(20) unsigned NOT NULL DEFAULT '0',
  `last_ip` varchar(128) NOT NULL,
  `created_at` bigint(20) unsigned NOT NULL DEFAULT '0',
  `updated_at` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `username` (`username`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8 ROW_FORMAT=COMPACT;

-- ----------------------------
-- Records of users
-- ----------------------------
INSERT INTO `users` VALUES ('1', 'test', 'n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg=', '', '0', '', '0', '0');
SET FOREIGN_KEY_CHECKS=1;

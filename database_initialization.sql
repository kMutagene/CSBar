-- Adminer 4.7.4 MySQL dump

SET NAMES utf8;
SET time_zone = '+00:00';
SET foreign_key_checks = 0;
SET sql_mode = 'NO_AUTO_VALUE_ON_ZERO';

SET NAMES utf8mb4;

DROP DATABASE IF EXISTS `CSBarDB`;
CREATE DATABASE `CSBarDB` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `CSBarDB`;

DELIMITER ;;

CREATE PROCEDURE `createTrade`(IN `Name` varchar(50) CHARACTER SET 'utf8', IN `Price` double, IN `Category` varchar(50) CHARACTER SET 'utf8', IN `ExtId` longtext)
INSERT INTO `Trade` VALUES (NULL,Name,Price,Category,ExtId);;

CREATE PROCEDURE `createUser`(IN `Pin` int, IN `UserName` varchar(50), IN `Email` varchar(50), IN `Department` varchar(50))
BEGIN

INSERT INTO `Person` Values (NULL,Pin,UserName,Email,1);
INSERT INTO `PersonInfos` (`FK_Person`,`Attribute`,`Value`) 
  VALUES(
  (SELECT Id FROM `Person` WHERE `Name` = UserName),
  'AG',
  Department);

END;;

CREATE PROCEDURE `debit`(IN `UserName` varchar(50), IN `Amount_Euro` int)
BEGIN

INSERT INTO `Orders` VALUES 
  (
  (SELECT `ID` FROM `Person` WHERE `Name` = UserName),
  (SELECT `ID` FROM `Trade` WHERE `Name` = 'Debit'),
  (Amount_Euro),
  (NOW())
  );

END;;

CREATE PROCEDURE `deposit`(IN `UserName` varchar(50), IN `Amount_Euro` int)
BEGIN

INSERT INTO `Orders` VALUES 
  (
  (SELECT `ID` FROM `Person` WHERE `Name` = UserName),
  (SELECT `ID` FROM `Trade` WHERE `Name` = 'Deposit'),
  (Amount_Euro),
  (NOW())
  );

END;;

CREATE PROCEDURE `getBalance`(IN `UserName` varchar(50))
BEGIN  
  SELECT Balance FROM `CurrentBalance` WHERE `FK_Person` = (SELECT `Id` FROM `Person` WHERE `Name` = UserName);
END;;

CREATE PROCEDURE `insertOrderByBarcode`(IN `UserName` varchar(50), IN `Barcode` varchar(50), IN `Amount` int)
INSERT INTO `Orders` VALUES (
  (SELECT Id FROM `Person` WHERE `Name` = UserName),
  (SELECT Id FROM `Trade` WHERE `ExtId` = Barcode),
  Amount,
  NOW()
);;

CREATE PROCEDURE `insertOrderByName`(IN `UserName` varchar(50), IN `TradeName` varchar(50), IN `Amount` int(11))
INSERT INTO `Orders` VALUES (
  (SELECT Id FROM `Person` WHERE `Name` = UserName),
  (SELECT Id FROM `Trade` WHERE `Name` = TradeName),
  Amount,
  NOW()
  );;

CREATE PROCEDURE `updatePrice`(IN `TradeName` varchar(50), IN `NewPrice` double)
BEGIN

INSERT INTO `Trade`
  VALUES (
  NULL, 
  TradeName,
  NewPrice,
  (SELECT Category FROM (SELECT * FROM `Trade`) AS something WHERE `Name` = TradeName),
  (SELECT ExtId FROM (SELECT * FROM `Trade`) AS something WHERE `Name` = TradeName)
);

UPDATE `Trade`
SET
   `Name` = CONCAT(Name,'OutDated'),
   `ExtId` = CONCAT(ExtId,'OutDated')
WHERE (`Name` = TradeName)
AND NOT (`Price` = NewPrice);

END;;

DELIMITER ;

DROP VIEW IF EXISTS `CurrentBalance`;
CREATE TABLE `CurrentBalance` (`FK_Person` bigint(20), `Name` varchar(50), `Pin` int(11), `Balance` double);


DROP VIEW IF EXISTS `OrderInfo`;
CREATE TABLE `OrderInfo` (`Amount` int(11), `Time` datetime(6), `Name` varchar(50), `Price` double, `Category` varchar(50), `Attribute` varchar(50), `Value` varchar(50), `PersonName` varchar(50));


DROP TABLE IF EXISTS `Orders`;
CREATE TABLE `Orders` (
  `FK_Person` bigint(20) NOT NULL,
  `FK_Trade` bigint(20) NOT NULL,
  `Amount` int(11) NOT NULL,
  `Time` datetime(6) NOT NULL,
  KEY `FK_Person` (`FK_Person`),
  KEY `FK_Trade` (`FK_Trade`),
  CONSTRAINT `Orders_ibfk_1` FOREIGN KEY (`FK_Person`) REFERENCES `Person` (`Id`),
  CONSTRAINT `Orders_ibfk_2` FOREIGN KEY (`FK_Trade`) REFERENCES `Trade` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `Orders` (`FK_Person`, `FK_Trade`, `Amount`, `Time`) VALUES
(1,	1,	1,	'2019-11-09 17:13:12.000000');

DROP TABLE IF EXISTS `Person`;
CREATE TABLE `Person` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Pin` int(11) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Email` varchar(50) NOT NULL,
  `Status` tinyint(3) unsigned NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_PersonPin` (`Id`),
  UNIQUE KEY `Pin` (`Pin`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `Person` (`Id`, `Pin`, `Name`, `Email`, `Status`) VALUES
(1,	1234,	'TestUser',	'ex@ample.de',	1);

DROP TABLE IF EXISTS `PersonInfos`;
CREATE TABLE `PersonInfos` (
  `FK_Person` bigint(20) NOT NULL,
  `Attribute` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  `Value` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci DEFAULT NULL,
  KEY `FK_Person` (`FK_Person`),
  CONSTRAINT `PersonInfos_ibfk_1` FOREIGN KEY (`FK_Person`) REFERENCES `Person` (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `PersonInfos` (`FK_Person`, `Attribute`, `Value`) VALUES
(1,	'AG',	'TestDepartment');

DROP TABLE IF EXISTS `Trade`;
CREATE TABLE `Trade` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Name` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  `Price` double NOT NULL,
  `Category` varchar(50) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL,
  `ExtId` longtext NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `Trade` (`Id`, `Name`, `Price`, `Category`, `ExtId`) VALUES
(1,	'checkBalance',	0,	'TestStuff',	'7610095003003'),
(2,	'Deposit',	-1,	'Deposit',	'cace0f69-1748-439c-a2e9-58449a6af8fb'),
(3, 'Coffee',  0.3,  'Coffee',  ''),
(4, 'Debit',  1,  'Debit',  'a0b11e0b-4dcb-4a06-a7c3-77e0fb0a23da');

DROP TABLE IF EXISTS `CurrentBalance`;
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `CurrentBalance` AS select `Orders`.`FK_Person` AS `FK_Person`,`Person`.`Name` AS `Name`,`Person`.`Pin` AS `Pin`,sum((`Orders`.`Amount` * `Trade`.`Price`)) AS `Balance` from ((`Orders` join `Trade` on((`Orders`.`FK_Trade` = `Trade`.`Id`))) join `Person` on((`Orders`.`FK_Person` = `Person`.`Id`))) group by `Orders`.`FK_Person`,`Person`.`Name`,`Person`.`Pin`;

DROP TABLE IF EXISTS `OrderInfo`;
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`localhost` SQL SECURITY DEFINER VIEW `OrderInfo` AS select `Orders`.`Amount` AS `Amount`,`Orders`.`Time` AS `Time`,`Trade`.`Name` AS `Name`,`Trade`.`Price` AS `Price`,`Trade`.`Category` AS `Category`,`PersonInfos`.`Attribute` AS `Attribute`,`PersonInfos`.`Value` AS `Value`,`Person`.`Name` AS `PersonName` from (((`Orders` join `Person` on((`Orders`.`FK_Person` = `Person`.`Id`))) join `PersonInfos` on((`Person`.`Id` = `PersonInfos`.`FK_Person`))) join `Trade` on((`Orders`.`FK_Trade` = `Trade`.`Id`))) order by `Orders`.`Time` desc;

-- 2019-11-09 17:18:30

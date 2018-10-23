

<?php
require_once('./PHPMailer/PHPMailerAutoload.php'); //引入phpMailer 記得將路徑換成您自己的path
echo "aaa";
$mail= new PHPMailer(); //初始化一個PHPMailer物件
$mail->Host = "smtp.gmail.com"; //SMTP主機 (這邊以gmail為例，所以填寫gmail stmp)
$mail->IsSMTP(); //設定使用SMTP方式寄信
$mail->SMTPAuth = true; //啟用SMTP驗證模式
$mail->Username = "eddie0457@gmail.com"; //您的 gamil 帳號
$mail->Password = "06091225"; //您的 gmail 密碼
$mail->SMTPSecure = "ssl"; // SSL連線 (要使用gmail stmp需要設定ssl模式) 
$mail->Port = 465; //Gamil的SMTP主機的port(Gmail為465)。
$mail->CharSet = "utf-8"; //郵件編碼
  
$mail->From = "eddie0457@gmail.com"; //寄件者信箱
$mail->FromName = "BuildServer"; //寄件者姓名
$mail->AddAddress("eddie1_huang@protech.com.tw", "Eddie"); //收件人郵件和名稱
//$mail->AddBCC('cc@example.com'); //設定 密件副本收件人 

$mail->IsHTML(true); //郵件內容為html 
//$mail->addAttachment('/tmp/image.jpg', 'new.jpg'); //添加附件(若不需要則註解掉就好)
 
$mail->Subject = "主題-測試郵件"; //郵件標題
$mail->Body ="內容-測試test123"; //郵件內容
$mail->AltBody = '當收件人的電子信箱不支援html時，會顯示這串~~';
 
echo "bbb";
if(!$mail->send()) {
    echo "ggg";
    echo '信件發送失敗!!';    
    echo 'Mailer Error: ' . $mail->ErrorInfo;
} else {    
    echo "ppp";
    echo '信件已發送!!';
}
?>
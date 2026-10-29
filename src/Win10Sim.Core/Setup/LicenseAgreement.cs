namespace Win10Sim.Core.Setup;

/// <summary>
/// Текст лицензионного соглашения. Лежит в ядре, а не в XAML: это контент
/// экрана, его длина и переносы не должны зависеть от вёрстки.
/// Это стилизованный текст, а не копия настоящей лицензии Microsoft.
/// </summary>
public static class LicenseAgreement
{
    public static string GetText(string culture) =>
        culture.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? Russian : English;

    private const string Russian =
"""
ЛИЦЕНЗИОННОЕ СОГЛАШЕНИЕ НА ПРОГРАММНОЕ ОБЕСПЕЧЕНИЕ WINDOWS 10

Настоящий документ является демонстрационным текстом, имитирующим экран
принятия лицензии в программе установки Windows 10. Он не является
юридическим документом и не предоставляет никаких прав на программное
обеспечение Microsoft.

1. ПРЕДМЕТ СОГЛАШЕНИЯ

1.1. Вам предоставляется ограниченное право установки и использования
одного экземпляра операционной системы Windows 10 на одном физическом
компьютере или в одной виртуальной машине.

1.2. Редакция операционной системы определяется ключом продукта либо
выбором на предыдущем экране программы установки.

2. ПРАВА И ОГРАНИЧЕНИЯ

2.1. Запрещается распространять, продавать, сдавать в аренду или иным
образом передавать экземпляр операционной системы третьим лицам.

2.2. Запрещается reverse engineering, декомпиляция и дизассемблирование
компонентов операционной системы, кроме случаев, прямо разрешённых
применимым законодательством.

2.3. Запрещается использовать компоненты системы для создания вредоносного
программного обеспечения либо для обхода технических средств защиты.

3. АКТИВАЦИЯ И ОБНОВЛЕНИЯ

3.1. Для использования всех возможностей операционной системы требуется
активация ключом продукта либо цифровой лицензией, привязанной к учётной
записи.

3.2. Операционная система может автоматически загружать и устанавливать
обновления безопасности и исправления. Отключение обновлений может снизить
уровень защиты компьютера.

4. ПЕРСОНАЛЬНЫЕ ДАННЫЕ И ДИАГНОСТИКА

4.1. Система передаёт ограниченный набор диагностических сведений,
необходимых для поддержания работоспособности и безопасности устройства.

4.2. Параметры конфиденциальности можно изменить в разделе
«Параметры → Конфиденциальность» после завершения установки.

5. ОТКАЗ ОТ ГАРАНТИЙ

5.1. Программное обеспечение предоставляется «как есть». Изготовитель не
предоставляет гарантий, явных или подразумеваемых, включая гарантии
товарной пригодности и пригодности для конкретной цели.

6. ОГРАНИЧЕНИЕ ОТВЕТСТВЕННОСТИ

6.1. Изготовитель не несёт ответственности за косвенный, случайный или
штрафной ущерб, возникший в результате использования или невозможности
использования программного обеспечения.

7. ПРЕКРАЩЕНИЕ ДЕЙСТВИЯ

7.1. Настоящее соглашение действует до его прекращения. Оно прекращается
автоматически при нарушении любого из условий либо при удалении
операционной системы с устройства.

8. ЗАКЛЮЧИТЕЛЬНЫЕ ПОЛОЖЕНИЯ

8.1. Нажимая «Далее» после установки флажка «Я принимаю условия
лицензионного соглашения», вы подтверждаете, что прочитали и поняли
настоящие условия и согласны их соблюдать.

8.2. Если вы не согласны с условиями, нажмите «Назад» и завершите работу
программы установки, не устанавливая операционную систему.

Для продолжения установки прокрутите текст до конца и установите флажок
принятия условий.
""";

    private const string English =
"""
MICROSOFT SOFTWARE LICENSE TERMS — WINDOWS 10

This document is demonstration text that imitates the license acceptance
screen of the Windows 10 setup program. It is not a legal document and
grants no rights to any Microsoft software.

1. SCOPE OF THE AGREEMENT

1.1. You are granted a limited right to install and use one copy of the
Windows 10 operating system on one physical computer or in one virtual
machine.

1.2. The edition of the operating system is determined by the product key
or by the selection made on the previous setup screen.

2. RIGHTS AND RESTRICTIONS

2.1. You may not distribute, sell, rent or otherwise transfer the copy of
the operating system to third parties.

2.2. You may not reverse engineer, decompile or disassemble components of
the operating system, except where expressly permitted by applicable law.

2.3. You may not use the components of the system to create malicious
software or to circumvent technical protection measures.

3. ACTIVATION AND UPDATES

3.1. Full functionality requires activation with a product key or a digital
license bound to an account.

3.2. The operating system may automatically download and install security
updates and fixes. Disabling updates may reduce the security of the device.

4. PERSONAL DATA AND DIAGNOSTICS

4.1. The system transmits a limited set of diagnostic data required to keep
the device working and secure.

4.2. Privacy settings can be changed in Settings → Privacy after setup is
complete.

5. DISCLAIMER OF WARRANTIES

5.1. The software is provided "as is". The manufacturer gives no express or
implied warranties, including fitness for a particular purpose.

6. LIMITATION OF LIABILITY

6.1. The manufacturer is not liable for indirect, incidental or punitive
damages arising from the use or inability to use the software.

7. TERMINATION

7.1. This agreement remains in effect until terminated. It terminates
automatically upon any breach or upon removal of the operating system from
the device.

8. FINAL PROVISIONS

8.1. By selecting "Next" after checking "I accept the license terms", you
confirm that you have read and understood these terms and agree to comply
with them.

8.2. If you do not agree to the terms, select "Back" and exit the setup
program without installing the operating system.

To continue setup, scroll to the end of the text and select the acceptance
check box.
""";
}

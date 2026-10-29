// Windows 10 Simulator — вариант C++ / SFML 2.6 (для сравнения с WPF).
// Сборка (MSVC): cl /std:c++17 /EHsc main.cpp /link sfml-graphics.lib sfml-window.lib sfml-system.lib
// Сборка (MinGW): g++ -std=c++17 main.cpp -lsfml-graphics -lsfml-window -lsfml-system
//
// Полноэкранный безрамочный режим:
//   sf::Style::None  — окно без рамки/заголовка/кнопок;
//   видеорежим Desktop — окно на весь текущий рабочий стол (без смены разрешения),
//   в отличие от Style::Fullscreen, который переключает видеорежим и провоцирует мигание.
// Выход: sf::Keyboard::Insert (проверяется в цикле событий и в sf::Event::KeyPressed).

#include <SFML/Graphics.hpp>
#include <cmath>
#include <string>

namespace
{
    // Кольцо из 5 точек: 1.6 с на оборот, 60 FPS.
    constexpr int   kDots        = 5;
    constexpr float kRingRadius  = 26.f;
    constexpr float kDotRadius   = 4.f;
    constexpr float kDegPerFrame = 3.75f;

    void centerText(sf::Text& text, float x, float y)
    {
        const sf::FloatRect b = text.getLocalBounds();
        text.setOrigin(b.left + b.width / 2.f, b.top + b.height / 2.f);
        text.setPosition(x, y);
    }
}

int main()
{
    // Desktop = полный экран текущего разрешения, без exclusive mode.
    sf::VideoMode mode = sf::VideoMode::getDesktopMode();
    sf::RenderWindow window(mode, "Windows 10 Simulator", sf::Style::None);
    window.setVerticalSyncEnabled(true);
    window.setMouseCursorVisible(true);

    sf::Font font;
    if (!font.loadFromFile("C:/Windows/Fonts/segoeui.ttf"))
        return 1; // без Segoe UI интерфейс не имеет смысла — лучше упасть явно

    sf::RectangleShape tile(sf::Vector2f(46.f, 46.f));
    tile.setFillColor(sf::Color::White);

    sf::CircleShape dot(kDotRadius);
    dot.setOrigin(kDotRadius, kDotRadius);

    sf::Text title("Windows 10 Simulator", font, 44);
    title.setFillColor(sf::Color::White);

    sf::Text hint("Insert - exit to desktop", font, 22);
    hint.setFillColor(sf::Color(110, 110, 110));

    const float cx = static_cast<float>(mode.width) / 2.f;
    const float cy = static_cast<float>(mode.height) / 2.f;
    const float flashCenterY = cy - 150.f;

    centerText(title, cx, cy + 30.f);
    centerText(hint, cx, static_cast<float>(mode.height) - 60.f);

    float angle = 0.f;
    sf::Clock clock;

    while (window.isOpen())
    {
        sf::Event event{};
        while (window.pollEvent(event))
        {
            if (event.type == sf::Event::Closed)
            {
                // Закрытие по Alt+F4/крестику максимально затруднено (рамки нет),
                // но всё равно не даём окну закрыться иначе как по Insert.
                continue;
            }

            if (event.type == sf::Event::KeyPressed &&
                event.key.code == sf::Keyboard::Insert)
            {
                window.close();
            }
        }

        angle += kDegPerFrame;

        window.clear(sf::Color::Black);

        // Логотип: 4 квадрата
        const float gap = 8.f;
        const float left = cx - 46.f - gap / 2.f;
        const float top  = flashCenterY - 46.f - gap / 2.f;
        for (int r = 0; r < 2; ++r)
        {
            for (int c = 0; c < 2; ++c)
            {
                tile.setPosition(left + c * (46.f + gap), top + r * (46.f + gap));
                window.draw(tile);
            }
        }

        // Точки
        for (int i = 0; i < kDots; ++i)
        {
            const float rad = (angle + i * 72.f) * 3.14159265f / 180.f;
            dot.setPosition(cx + std::cos(rad) * kRingRadius,
                            flashCenterY + 110.f + std::sin(rad) * kRingRadius);
            dot.setFillColor(sf::Color(255, 255, 255,
                                       static_cast<sf::Uint8>(255 - i * 46)));
            window.draw(dot);
        }

        window.draw(title);
        window.draw(hint);
        window.display();
    }

    return 0;
}

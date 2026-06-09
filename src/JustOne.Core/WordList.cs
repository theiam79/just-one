namespace JustOne.Core;

/// <summary>Built-in deck of mystery words, in the spirit of the original game's cards.</summary>
public static class WordList
{
    public static readonly IReadOnlyList<string> Words =
    [
        // Animals
        "Elephant", "Penguin", "Kangaroo", "Dolphin", "Octopus", "Giraffe", "Tiger", "Panda",
        "Shark", "Eagle", "Spider", "Butterfly", "Camel", "Wolf", "Owl", "Flamingo",
        "Hedgehog", "Squirrel", "Lobster", "Jellyfish", "Crocodile", "Gorilla", "Hamster", "Peacock",
        "Scorpion", "Whale", "Zebra", "Unicorn", "Dragon", "Dinosaur",
        // Food & drink
        "Pizza", "Sushi", "Chocolate", "Banana", "Croissant", "Cheese", "Pancake", "Spaghetti",
        "Taco", "Honey", "Coconut", "Avocado", "Pretzel", "Mustard", "Popcorn", "Waffle",
        "Lemonade", "Espresso", "Marshmallow", "Pickle", "Bacon", "Donut", "Cupcake", "Garlic",
        "Watermelon", "Champagne", "Yogurt", "Burrito", "Ketchup", "Olive",
        // Objects
        "Umbrella", "Telescope", "Anchor", "Candle", "Mirror", "Compass", "Hammer", "Ladder",
        "Balloon", "Backpack", "Helmet", "Wallet", "Scissors", "Toothbrush", "Microscope", "Trampoline",
        "Parachute", "Boomerang", "Skateboard", "Telephone", "Keyboard", "Magnet", "Whistle", "Suitcase",
        "Blanket", "Bucket", "Crayon", "Stapler", "Hourglass", "Lighthouse",
        // Places
        "Paris", "Egypt", "Hollywood", "Antarctica", "Amazon", "Hawaii", "Venice", "Tokyo",
        "Sahara", "Atlantis", "Everest", "Vegas", "London", "Rome", "Australia", "Iceland",
        "Niagara", "Disneyland", "Bermuda", "Stonehenge",
        // People & characters
        "Batman", "Einstein", "Cleopatra", "Shakespeare", "Sherlock", "Tarzan", "Cinderella", "Pikachu",
        "Superman", "Mozart", "Picasso", "Gandalf", "Dracula", "Santa", "Elvis", "Napoleon",
        "Frankenstein", "Godzilla", "Yoda", "Mario", "Hercules", "Merlin", "Shrek", "Pinocchio",
        "Aladdin",
        // Nature
        "Volcano", "Rainbow", "Glacier", "Tornado", "Desert", "Jungle", "Waterfall", "Thunder",
        "Eclipse", "Tsunami", "Aurora", "Meteor", "Coral", "Bamboo", "Cactus", "Lava",
        "Quicksand", "Avalanche", "Canyon", "Oasis",
        // Sports & activities
        "Karate", "Yoga", "Marathon", "Surfing", "Chess", "Poker", "Bowling", "Archery",
        "Juggling", "Karaoke", "Ballet", "Skiing", "Boxing", "Golf", "Tennis", "Rugby",
        "Gymnastics", "Fishing", "Camping", "Origami",
        // Professions & figures
        "Astronaut", "Pirate", "Ninja", "Detective", "Magician", "Firefighter", "Surgeon", "Clown",
        "Spy", "Chef", "Cowboy", "Plumber", "Librarian", "Barista", "Lifeguard", "Architect",
        "Pilot", "Samurai", "Wizard", "Vampire",
        // Concepts
        "Gravity", "Karma", "Infinity", "Silence", "Luck", "Dream", "Shadow", "Echo",
        "Whisper", "Memory", "Jealousy", "Courage", "Sarcasm", "Gossip", "Hiccup", "Yawn",
        "Sneeze", "Tickle", "Wink", "Selfie",
        // Transport
        "Helicopter", "Submarine", "Gondola", "Tractor", "Ambulance", "Rocket", "Canoe", "Scooter",
        "Limousine", "Zeppelin", "Subway", "Ferry", "Tricycle", "Bulldozer", "Taxi",
        // Entertainment & music
        "Circus", "Opera", "Tattoo", "Pyramid", "Mummy", "Robot", "Zombie", "Alien",
        "Ghost", "Treasure", "Maze", "Puzzle", "Festival", "Carnival", "Fireworks", "Disco",
        "Jukebox", "Harmonica", "Ukulele", "Bagpipes", "Violin", "Trumpet", "Saxophone", "Emoji",
        // Clothing
        "Tuxedo", "Bikini", "Kimono", "Sombrero", "Stiletto", "Pajamas", "Mittens", "Tiara",
        "Cape", "Overalls",
        // Household
        "Refrigerator", "Microwave", "Vacuum", "Chimney", "Hammock", "Aquarium", "Doorbell", "Curtain",
        "Pillow", "Toaster",
        // School & science
        "Algebra", "Atom", "Fossil", "Chemistry", "Recess", "Homework", "Diploma", "Eraser",
        "Globe", "Abacus",
        // Holidays & occasions
        "Halloween", "Christmas", "Easter", "Valentine", "Birthday", "Thanksgiving",
    ];
}

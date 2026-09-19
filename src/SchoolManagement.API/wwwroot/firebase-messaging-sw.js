importScripts(
    "https://www.gstatic.com/firebasejs/10.13.2/firebase-app-compat.js"
);

importScripts(
    "https://www.gstatic.com/firebasejs/10.13.2/firebase-messaging-compat.js"
);

firebase.initializeApp({
    apiKey: "AIzaSyBHoVSsN35vXoq4H3K7rO_l9jL523SA0yk",
    authDomain: "school-management-system-2aebd.firebaseapp.com",
    projectId: "school-management-system-2aebd",
    storageBucket: "school-management-system-2aebd.firebasestorage.app",
    messagingSenderId: "283178135252",
    appId: "1:283178135252:web:b90f6a8bf4b94c73645541",
    measurementId: "G-Y9D5W9P9TW"
});

const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
    console.log(
        "Background Firebase message:",
        payload
    );

    const title =
        payload.notification?.title ??
        "School Management";

    const options = {
        body:
            payload.notification?.body ??
            "You have a new notification.",

        data:
            payload.data
    };

    self.registration.showNotification(
        title,
        options
    );
});
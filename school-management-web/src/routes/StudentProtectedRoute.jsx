import {
    Navigate,
} from "react-router-dom";


export default function StudentProtectedRoute({
    children,
}) {

    const token =
        localStorage.getItem(
            "accessToken"
        );


    const storedStudent =
        localStorage.getItem(
            "studentUser"
        );


    if (
        !token ||
        !storedStudent
    ) {

        return (
            <Navigate
                to="/student/login"
                replace
            />
        );

    }


    try {

        const student =
            JSON.parse(
                storedStudent
            );


        const roles =
            student?.roles ??
            [];


        if (
            !roles.includes(
                "Student"
            )
        ) {

            return (
                <Navigate
                    to="/student/login"
                    replace
                />
            );

        }

    }
    catch {

        localStorage.removeItem(
            "accessToken"
        );

        localStorage.removeItem(
            "studentUser"
        );


        return (
            <Navigate
                to="/student/login"
                replace
            />
        );

    }


    return children;

}
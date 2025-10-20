import { supabase } from "../lib/supabaseClient";
import { useEffect } from "react";

export default function TestLogin() {
  useEffect(() => {
    const loginAndFetch = async () => {
      // Step 1: Login
      const { data, error } = await supabase.auth.signInWithPassword({
        email: "abdelrhmangad197@gmail.com",
        password: "trial1234",
      });

      if (error) {
        console.error("Login error:", error);
        return;
      }

      const token = data.session.access_token;
      console.log("✅ Access token:", token);
      console.log("🔍 Token length:", token.length);
      console.log("🔍 Token parts:", token.split('.').length);
      console.log("🔍 Complete token for copy:", token);

      // Step 2: Call CatalogService with Authorization header
      try {
        console.log("🔍 Testing CatalogService (port 5179)...");
        const catalogResponse = await fetch("http://localhost:5179/api/books", {
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
        });

        console.log("📊 CatalogService response status:", catalogResponse.status);
        
        if (!catalogResponse.ok) {
          const errorText = await catalogResponse.text();
          console.error("❌ CatalogService error:", errorText);
        } else {
          const catalogResult = await catalogResponse.json();
          console.log("📚 CatalogService API response:", catalogResult);
        }
      } catch (err) {
        console.error("❌ Error calling CatalogService:", err);
      }

      // Step 3: Call OrderService with Authorization header
      try {
        console.log("🔍 Testing OrderService (port 5180)...");
        const orderResponse = await fetch("http://localhost:5180/api/orders", {
          headers: {
            Authorization: `Bearer ${token}`,
            "Content-Type": "application/json",
          },
        });

        console.log("📊 OrderService response status:", orderResponse.status);
        
        if (!orderResponse.ok) {
          const errorText = await orderResponse.text();
          console.error("❌ OrderService error:", errorText);
        } else {
          const orderResult = await orderResponse.json();
          console.log("📦 OrderService API response:", orderResult);
        }
      } catch (err) {
        console.error("❌ Error calling OrderService:", err);
      }
    };

    loginAndFetch();
  }, []);

  return <div>Check console for token and backend response</div>;
}
